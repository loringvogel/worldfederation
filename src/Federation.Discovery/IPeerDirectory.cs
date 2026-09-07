using Federation.Identity;
using Federation.Protocol;

namespace Federation.Discovery;

/// <summary>Maintains a local directory of known peers.</summary>
public interface IPeerDirectory
{
    Task AddPeerAsync(PeerRecord peerRecord, CancellationToken ct = default);
    Task<PeerRecord?> GetPeerAsync(DeviceId deviceId, CancellationToken ct = default);
    Task<IReadOnlyList<PeerRecord>> GetAllPeersAsync(CancellationToken ct = default);
}
