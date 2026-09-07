using System.Collections.Concurrent;
using Federation.Protocol;

namespace Federation.Identity;

/// <summary>In-memory implementation of IIdentityStore for development and testing.</summary>
public sealed class InMemoryIdentityStore : IIdentityStore
{
    private readonly ConcurrentDictionary<DeviceId, DeviceIdentity> _identities = new();
    private readonly ConcurrentDictionary<DeviceId, PeerRecord> _peerRecords = new();

    public Task StoreDeviceIdentityAsync(DeviceIdentity identity, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(identity);
        _identities[identity.Id] = identity;
        return Task.CompletedTask;
    }

    public Task<DeviceIdentity?> LoadDeviceIdentityAsync(DeviceId deviceId, CancellationToken ct = default)
    {
        _identities.TryGetValue(deviceId, out var identity);
        return Task.FromResult(identity);
    }

    public Task StorePeerRecordAsync(PeerRecord peerRecord, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(peerRecord);
        _peerRecords[peerRecord.DeviceId] = peerRecord;
        return Task.CompletedTask;
    }

    public Task<PeerRecord?> GetPeerRecordAsync(DeviceId deviceId, CancellationToken ct = default)
    {
        _peerRecords.TryGetValue(deviceId, out var record);
        return Task.FromResult(record);
    }

    public Task<IReadOnlyList<PeerRecord>> GetAllPeerRecordsAsync(CancellationToken ct = default)
    {
        IReadOnlyList<PeerRecord> result = _peerRecords.Values.ToList();
        return Task.FromResult(result);
    }
}
