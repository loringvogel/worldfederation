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

/// <summary>In-memory implementation of ILocalMessageCache for development and testing.</summary>
public sealed class InMemoryLocalMessageCache : ILocalMessageCache
{
    private readonly ConcurrentDictionary<(RoomId, DiscussionId), List<CachedMessage>> _cache = new();

    public Task StoreAsync(MessageId messageId, RoomId roomId, DiscussionId discussionId,
        DeviceId senderDeviceId, MessageType type, int round,
        byte[] contentEncrypted, CancellationToken ct = default)
    {
        var key = (roomId, discussionId);
        var list = _cache.GetOrAdd(key, _ => new List<CachedMessage>());
        lock (list)
        {
            list.Add(new CachedMessage
            {
                MessageId = messageId,
                SenderDeviceId = senderDeviceId,
                Type = type,
                Round = round,
                ContentEncrypted = contentEncrypted,
                ReceivedAt = DateTimeOffset.UtcNow,
            });
        }
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<CachedMessage>> GetMessagesAsync(RoomId roomId, DiscussionId discussionId, CancellationToken ct = default)
    {
        var key = (roomId, discussionId);
        if (!_cache.TryGetValue(key, out var list))
            return Task.FromResult<IReadOnlyList<CachedMessage>>(Array.Empty<CachedMessage>());
        lock (list)
        {
            return Task.FromResult<IReadOnlyList<CachedMessage>>(list.ToList());
        }
    }

    public Task WipeAsync(CancellationToken ct = default)
    {
        _cache.Clear();
        return Task.CompletedTask;
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
