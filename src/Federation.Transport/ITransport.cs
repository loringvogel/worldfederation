namespace Federation.Transport;

/// <summary>Transport layer for direct peer-to-peer connections.</summary>
public interface ITransport
{
    Task<IPeerConnection> ConnectAsync(string address, CancellationToken ct = default);

    /// <summary>
    /// Listens for incoming connections on the given port.
    /// Note: nodes do NOT expose inbound internet ports; ListenAsync is for local/LAN use only.
    /// </summary>
    IAsyncEnumerable<IPeerConnection> ListenAsync(int port, CancellationToken ct);
}
