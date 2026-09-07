using System.Collections.Concurrent;
using Federation.Protocol;

namespace Federation.Node;

/// <summary>
/// Stores decrypted messages on the local node. This is the trusted local cache
/// that agents read from. Never transmitted to the relay.
/// </summary>
public sealed class InMemoryLocalCache
{
    private readonly ConcurrentDictionary<(RoomId, DiscussionId), SortedList<long, DecryptedMessage>> _cache = new();

    /// <summary>Stores a decrypted message indexed by room, discussion, and cursor.</summary>
    public void Store(RoomId roomId, DiscussionId discussionId, long cursor, DecryptedMessage message)
    {
        var key = (roomId, discussionId);
        var list = _cache.GetOrAdd(key, _ => new SortedList<long, DecryptedMessage>());

        lock (list)
        {
            list[cursor] = message;
        }
    }

    /// <summary>Retrieves decrypted messages for a discussion after a given cursor.</summary>
    public IReadOnlyList<DecryptedMessage> GetMessages(RoomId roomId, DiscussionId discussionId, long afterCursor = 0)
    {
        var key = (roomId, discussionId);
        if (!_cache.TryGetValue(key, out var list))
        {
            return [];
        }

        lock (list)
        {
            return list.Where(kvp => kvp.Key > afterCursor).Select(kvp => kvp.Value).ToList();
        }
    }
}

/// <summary>A decrypted council message as stored in the local node cache.</summary>
public sealed record DecryptedMessage
{
    public required MessageId MessageId { get; init; }
    public required DeviceId SenderDeviceId { get; init; }
    public required MessageType MessageType { get; init; }
    public required int Round { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required string Plaintext { get; init; }
    public required long Cursor { get; init; }
}
