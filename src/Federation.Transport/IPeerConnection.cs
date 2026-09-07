using Federation.Protocol;

namespace Federation.Transport;

/// <summary>A bidirectional connection to a peer device.</summary>
public interface IPeerConnection
{
    DeviceId RemoteDeviceId { get; }
    Task SendAsync(byte[] frame, CancellationToken ct = default);
    IAsyncEnumerable<byte[]> ReceiveFramesAsync(CancellationToken ct);
    Task CloseAsync(CancellationToken ct = default);
}
