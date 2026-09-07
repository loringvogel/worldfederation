using System.Collections.Concurrent;
using Federation.Protocol;

namespace Federation.Relay.Stores;

/// <summary>Thread-safe in-memory room repository for Phase 1 simulation.</summary>
public sealed class InMemoryRoomRepository : IRoomRepository
{
    private readonly ConcurrentDictionary<RoomId, RoomSummary> _rooms = new();
    private readonly IMembershipRepository _memberships;

    public InMemoryRoomRepository(IMembershipRepository memberships)
    {
        _memberships = memberships;
    }

    public async Task<RoomSummary> CreateRoomAsync(CreateRoomRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var roomId = RoomId.New();
        var summary = new RoomSummary
        {
            RoomId = roomId,
            Name = request.Name,
            CreatedAt = DateTimeOffset.UtcNow,
            MemberCount = 1,
            ActiveDiscussionCount = 0,
        };

        if (!_rooms.TryAdd(roomId, summary))
        {
            throw new InvalidOperationException("Failed to create room (ID collision).");
        }

        await _memberships.AddMembershipAsync(roomId, request.OwnerDeviceId, MemberRole.Owner, ct).ConfigureAwait(false);
        return summary;
    }

    public Task<RoomSummary?> GetRoomAsync(RoomId roomId, CancellationToken ct = default)
    {
        _rooms.TryGetValue(roomId, out var room);
        return Task.FromResult(room);
    }

    public Task<IReadOnlyList<RoomSummary>> ListRoomsAsync(CancellationToken ct = default)
    {
        IReadOnlyList<RoomSummary> result = _rooms.Values.ToList();
        return Task.FromResult(result);
    }
}
