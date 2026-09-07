using System.Collections.Concurrent;
using Council.Contracts;

namespace Council.Relay.Api.Stores;

/// <summary>Thread-safe in-memory membership repository for Phase 1 simulation.</summary>
public sealed class InMemoryMembershipRepository : IMembershipRepository
{
    private readonly ConcurrentDictionary<MembershipId, MembershipRecord> _memberships = new();

    public Task<MembershipRecord?> GetMembershipAsync(MembershipId membershipId, CancellationToken ct = default)
    {
        _memberships.TryGetValue(membershipId, out var record);
        return Task.FromResult(record);
    }

    public Task<MembershipRecord?> GetMembershipByDeviceAsync(RoomId roomId, DeviceId deviceId, CancellationToken ct = default)
    {
        var record = _memberships.Values
            .FirstOrDefault(m => m.RoomId == roomId && m.DeviceId == deviceId && m.IsActive);
        return Task.FromResult(record);
    }

    public Task<IReadOnlyList<MembershipRecord>> GetActiveMembershipsAsync(RoomId roomId, CancellationToken ct = default)
    {
        IReadOnlyList<MembershipRecord> result = _memberships.Values
            .Where(m => m.RoomId == roomId && m.IsActive)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<DeviceId>> GetDevicesForRoomAsync(RoomId roomId, CancellationToken ct = default)
    {
        IReadOnlyList<DeviceId> result = _memberships.Values
            .Where(m => m.RoomId == roomId && m.IsActive)
            .Select(m => m.DeviceId)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<MembershipRecord> AddMembershipAsync(RoomId roomId, DeviceId deviceId, MemberRole role, CancellationToken ct = default)
    {
        var record = new MembershipRecord
        {
            MembershipId = MembershipId.New(),
            RoomId = roomId,
            DeviceId = deviceId,
            Role = role,
            JoinedAt = DateTimeOffset.UtcNow,
        };

        if (!_memberships.TryAdd(record.MembershipId, record))
        {
            throw new InvalidOperationException("Failed to add membership (ID collision).");
        }

        return Task.FromResult(record);
    }

    public Task RevokeMembershipAsync(MembershipId membershipId, CancellationToken ct = default)
    {
        if (!_memberships.TryGetValue(membershipId, out var existing))
        {
            throw new InvalidOperationException($"Membership '{membershipId}' not found.");
        }

        _memberships[membershipId] = existing with { RevokedAt = DateTimeOffset.UtcNow };
        return Task.CompletedTask;
    }
}
