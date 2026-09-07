using System.Collections.Concurrent;
using Federation.Identity;
using Federation.Protocol;

namespace Federation.Discovery;

/// <summary>In-memory peer directory for development and testing.</summary>
public sealed class InMemoryPeerDirectory : IPeerDirectory
{
    private readonly ConcurrentDictionary<DeviceId, PeerRecord> _peers = new();

    public Task AddPeerAsync(PeerRecord peerRecord, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(peerRecord);
        _peers[peerRecord.DeviceId] = peerRecord;
        return Task.CompletedTask;
    }

    public Task<PeerRecord?> GetPeerAsync(DeviceId deviceId, CancellationToken ct = default)
    {
        _peers.TryGetValue(deviceId, out var record);
        return Task.FromResult(record);
    }

    public Task<IReadOnlyList<PeerRecord>> GetAllPeersAsync(CancellationToken ct = default)
    {
        IReadOnlyList<PeerRecord> result = _peers.Values.ToList();
        return Task.FromResult(result);
    }
}
