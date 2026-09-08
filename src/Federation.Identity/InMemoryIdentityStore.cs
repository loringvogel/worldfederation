using System.Collections.Concurrent;
using Federation.Protocol;

namespace Federation.Identity;

/// <summary>In-memory implementation of IIdentityStore for development and testing.</summary>
public sealed class InMemoryIdentityStore : IIdentityStore
{
    private readonly ConcurrentDictionary<DeviceId, DeviceIdentity> _identities = new();
    private readonly ConcurrentDictionary<DeviceId, PeerRecord> _peerRecords = new();
    private readonly ConcurrentDictionary<RoomId, HashSet<DeviceId>> _roomMemberships = new();

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

    public Task<IReadOnlyList<PeerRecord>> GetPeersForRoomAsync(RoomId roomId, CancellationToken ct = default)
    {
        if (!_roomMemberships.TryGetValue(roomId, out var deviceIds))
        {
            return Task.FromResult<IReadOnlyList<PeerRecord>>(Array.Empty<PeerRecord>());
        }

        var result = new List<PeerRecord>();
        lock (deviceIds)
        {
            foreach (var did in deviceIds)
            {
                if (_peerRecords.TryGetValue(did, out var record))
                {
                    result.Add(record);
                }
            }
        }

        return Task.FromResult<IReadOnlyList<PeerRecord>>(result);
    }

    public Task StorePeerRecordForRoomAsync(RoomId roomId, PeerRecord record, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        _peerRecords[record.DeviceId] = record;

        var set = _roomMemberships.GetOrAdd(roomId, _ => new HashSet<DeviceId>());
        lock (set)
        {
            set.Add(record.DeviceId);
        }

        return Task.CompletedTask;
    }
}
