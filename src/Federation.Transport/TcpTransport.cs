using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using Federation.Protocol;

namespace Federation.Transport;

/// <summary>
/// For LAN and direct connections only. Not suitable for internet-facing use without
/// additional authentication. Phase 3 will add QUIC via System.Net.Quic.
/// </summary>
public sealed class TcpTransport : ITransport
{
    public async Task<IPeerConnection> ConnectAsync(string address, CancellationToken ct = default)
    {
        // Parse address like "tcp://host:port"
        var uri = new Uri(address);
        var client = new TcpClient();
        await client.ConnectAsync(uri.Host, uri.Port, ct).ConfigureAwait(false);
        return new TcpPeerConnection(client, DeviceId.New()); // Remote device ID resolved after handshake in Phase 3
    }

    public async IAsyncEnumerable<IPeerConnection> ListenAsync(int port, [EnumeratorCancellation] CancellationToken ct)
    {
        var listener = new TcpListener(IPAddress.Any, port);
        listener.Start();

        try
        {
            while (!ct.IsCancellationRequested)
            {
                var client = await listener.AcceptTcpClientAsync(ct).ConfigureAwait(false);
                yield return new TcpPeerConnection(client, DeviceId.New());
            }
        }
        finally
        {
            listener.Stop();
        }
    }

    private sealed class TcpPeerConnection : IPeerConnection, IDisposable
    {
        private readonly TcpClient _client;
        private readonly NetworkStream _stream;

        public TcpPeerConnection(TcpClient client, DeviceId remoteDeviceId)
        {
            _client = client;
            _stream = client.GetStream();
            RemoteDeviceId = remoteDeviceId;
        }

        public DeviceId RemoteDeviceId { get; }

        public async Task SendAsync(byte[] frame, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(frame);
            var lengthPrefix = new byte[4];
            BinaryPrimitives.WriteInt32BigEndian(lengthPrefix, frame.Length);
            await _stream.WriteAsync(lengthPrefix, ct).ConfigureAwait(false);
            await _stream.WriteAsync(frame, ct).ConfigureAwait(false);
        }

        public async IAsyncEnumerable<byte[]> ReceiveFramesAsync([EnumeratorCancellation] CancellationToken ct)
        {
            var lengthBuffer = new byte[4];

            while (!ct.IsCancellationRequested)
            {
                var bytesRead = 0;
                while (bytesRead < 4)
                {
                    var read = await _stream.ReadAsync(lengthBuffer.AsMemory(bytesRead, 4 - bytesRead), ct).ConfigureAwait(false);
                    if (read == 0) yield break; // Connection closed
                    bytesRead += read;
                }

                var frameLength = BinaryPrimitives.ReadInt32BigEndian(lengthBuffer);
                var frameBuffer = new byte[frameLength];
                bytesRead = 0;

                while (bytesRead < frameLength)
                {
                    var read = await _stream.ReadAsync(frameBuffer.AsMemory(bytesRead, frameLength - bytesRead), ct).ConfigureAwait(false);
                    if (read == 0) yield break;
                    bytesRead += read;
                }

                yield return frameBuffer;
            }
        }

        public Task CloseAsync(CancellationToken ct = default)
        {
            Dispose();
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _stream.Dispose();
            _client.Dispose();
        }
    }
}
