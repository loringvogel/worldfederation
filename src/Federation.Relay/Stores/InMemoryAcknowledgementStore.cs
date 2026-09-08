using System.Collections.Concurrent;
using Federation.Protocol;

namespace Federation.Relay.Stores;

/// <summary>In-memory acknowledgement cursor store for development.</summary>
public sealed class InMemoryAcknowledgementStore : IAcknowledgementStore
{
    private readonly ConcurrentDictionary<(RoomId, DiscussionId), long> _cursors = new();

    public Task<long> GetCursorAsync(RoomId roomId, DiscussionId discussionId, CancellationToken ct = default)
    {
        _cursors.TryGetValue((roomId, discussionId), out var cursor);
        return Task.FromResult(cursor);
    }

    public Task SetCursorAsync(RoomId roomId, DiscussionId discussionId, long cursor, CancellationToken ct = default)
    {
        _cursors[(roomId, discussionId)] = cursor;
        return Task.CompletedTask;
    }
}
