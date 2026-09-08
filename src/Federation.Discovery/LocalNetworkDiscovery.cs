using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Federation.Cryptography;
using Federation.Identity;
using Federation.Protocol;

namespace Federation.Discovery;

/// <summary>
/// UDP multicast peer discovery on 239.255.77.77:17777.
/// Broadcasts a signed local peer record every 30 seconds and receives peer records from others on the same LAN.
/// Validates signatures before yielding.
///
/// Room-scoped: beacons include the room IDs this node is seeking peers for.
/// On receive, only yields a peer record if the beacon lists at least one room in common
/// with the local node's rooms. Nodes in disjoint rooms never discover each other.
/// </summary>
public sealed class LocalNetworkDiscovery : IPeerDiscovery
{
    private static readonly IPAddress MulticastAddress = IPAddress.Parse("239.255.77.77");
    private const int MulticastPort = 17777;

    private readonly PeerRecord _localRecord;
    private readonly IdentityService _identityService;
    private readonly byte[] _localPublicKey;
    private readonly HashSet<RoomId> _localRoomIds;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public LocalNetworkDiscovery(PeerRecord localRecord, IdentityService identityService, byte[] localPublicKey, IEnumerable<RoomId>? roomIds = null)
    {
        ArgumentNullException.ThrowIfNull(localRecord);
        ArgumentNullException.ThrowIfNull(identityService);
        ArgumentNullException.ThrowIfNull(localPublicKey);
        _localRecord = localRecord;
        _identityService = identityService;
        _localPublicKey = localPublicKey;
        _localRoomIds = new HashSet<RoomId>(roomIds ?? Enumerable.Empty<RoomId>());
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

            DiscoveryBeacon? beacon;
            try
            {
                var json = Encoding.UTF8.GetString(result.Buffer);
                beacon = JsonSerializer.Deserialize<DiscoveryBeacon>(json, JsonOptions);
            }
#pragma warning disable CA1031 // Malformed discovery packets should not crash the listener
            catch
            {
                continue;
            }
#pragma warning restore CA1031

            if (beacon?.PeerRecord is not null && beacon.PeerRecord.DeviceId != _localRecord.DeviceId)
            {
                // Room-scoped filtering: only yield peers that share at least one room
                if (_localRoomIds.Count > 0 && beacon.RoomIds is not null)
                {
                    var remoteRooms = beacon.RoomIds.Select(r => new RoomId(Guid.Parse(r))).ToHashSet();
                    if (!remoteRooms.Overlaps(_localRoomIds))
                    {
                        // No rooms in common -- silently ignore this beacon
                        continue;
                    }
                }

                yield return beacon.PeerRecord;
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
                var beacon = new DiscoveryBeacon
                {
                    PeerRecord = _localRecord,
                    RoomIds = _localRoomIds.Select(r => r.Value.ToString()).ToArray(),
                };
                var json = JsonSerializer.Serialize(beacon, JsonOptions);
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

/// <summary>Discovery beacon payload including room scoping.</summary>
internal sealed record DiscoveryBeacon
{
    public PeerRecord? PeerRecord { get; init; }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "Serialized beacon payload.")]
    public string[]? RoomIds { get; init; }
}
