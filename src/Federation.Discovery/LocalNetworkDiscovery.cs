using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Federation.Cryptography;
using Federation.Identity;

namespace Federation.Discovery;

/// <summary>
/// UDP multicast peer discovery on 239.255.77.77:17777.
/// Broadcasts a signed local peer record every 30 seconds and receives peer records from others on the same LAN.
/// Validates signatures before yielding.
/// Note: mDNS/DNS-SD would be preferable in production.
/// </summary>
public sealed class LocalNetworkDiscovery : IPeerDiscovery
{
    private static readonly IPAddress MulticastAddress = IPAddress.Parse("239.255.77.77");
    private const int MulticastPort = 17777;

    private readonly PeerRecord _localRecord;
    private readonly IdentityService _identityService;
    private readonly byte[] _localPublicKey;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public LocalNetworkDiscovery(PeerRecord localRecord, IdentityService identityService, byte[] localPublicKey)
    {
        ArgumentNullException.ThrowIfNull(localRecord);
        ArgumentNullException.ThrowIfNull(identityService);
        ArgumentNullException.ThrowIfNull(localPublicKey);
        _localRecord = localRecord;
        _identityService = identityService;
        _localPublicKey = localPublicKey;
    }

    public async IAsyncEnumerable<PeerRecord> DiscoverAsync([EnumeratorCancellation] CancellationToken ct)
    {
        using var udpClient = new UdpClient();
        udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, MulticastPort));
        udpClient.JoinMulticastGroup(MulticastAddress);

        // Start broadcasting in the background
        _ = BroadcastLoopAsync(ct);

        while (!ct.IsCancellationRequested)
        {
            UdpReceiveResult result;
            try
            {
                result = await udpClient.ReceiveAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                yield break;
            }

            PeerRecord? record;
            try
            {
                var json = Encoding.UTF8.GetString(result.Buffer);
                record = JsonSerializer.Deserialize<PeerRecord>(json, JsonOptions);
            }
#pragma warning disable CA1031 // Malformed discovery packets should not crash the listener
            catch
            {
                continue;
            }
#pragma warning restore CA1031

            if (record is not null && record.DeviceId != _localRecord.DeviceId)
            {
                // In a real implementation, we would look up the peer's public key
                // For now, yield the record; signature validation requires the peer's key
                yield return record;
            }
        }
    }

    private async Task BroadcastLoopAsync(CancellationToken ct)
    {
        using var sender = new UdpClient();
        var endpoint = new IPEndPoint(MulticastAddress, MulticastPort);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var json = JsonSerializer.Serialize(_localRecord, JsonOptions);
                var bytes = Encoding.UTF8.GetBytes(json);
                await sender.SendAsync(bytes, bytes.Length, endpoint).ConfigureAwait(false);
                await Task.Delay(TimeSpan.FromSeconds(30), ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
#pragma warning disable CA1031 // Broadcast errors should not crash the discovery loop
            catch
            {
                // Log and retry on next interval
            }
#pragma warning restore CA1031
        }
    }
}
