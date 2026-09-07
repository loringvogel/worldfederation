using System.Collections.Concurrent;
using Federation.Protocol;

namespace Federation.Relay.Stores;

/// <summary>Thread-safe in-memory message store for Phase 1 simulation.</summary>
public sealed class InMemoryMessageStore : IMessageStore
{
    private readonly ConcurrentDictionary<MessageId, MessageEnvelope> _envelopes = new();
    private readonly ConcurrentDictionary<(RoomId, DiscussionId), List<(long Cursor, MessageEnvelope Envelope)>> _byDiscussion = new();
    private long _cursor;

    public Task<long> StoreEnvelopeAsync(MessageEnvelope envelope, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        if (!_envelopes.TryAdd(envelope.MessageId, envelope))
        {
            throw new InvalidOperationException($"Duplicate message ID: {envelope.MessageId}");
        }

        var cursor = Interlocked.Increment(ref _cursor);
        var key = (envelope.RoomId, envelope.DiscussionId);
        var list = _byDiscussion.GetOrAdd(key, _ => []);

        lock (list)
        {
            list.Add((cursor, envelope));
        }

        return Task.FromResult(cursor);
    }

    public Task<GetEnvelopesResponse> GetEnvelopesAsync(RoomId roomId, DiscussionId discussionId, long afterCursor, CancellationToken ct = default)
    {
        var key = (roomId, discussionId);
        if (!_byDiscussion.TryGetValue(key, out var list))
        {
            return Task.FromResult(new GetEnvelopesResponse { Envelopes = [], Cursor = afterCursor });
        }

        List<MessageEnvelope> results;
        long maxCursor = afterCursor;

        lock (list)
        {
            results = [];
            foreach (var (cursor, envelope) in list)
            {
                if (cursor > afterCursor)
                {
                    results.Add(envelope);
                    if (cursor > maxCursor) maxCursor = cursor;
                }
            }
        }

        return Task.FromResult(new GetEnvelopesResponse { Envelopes = results, Cursor = maxCursor });
    }

    public Task<bool> ExistsAsync(MessageId messageId, CancellationToken ct = default)
    {
        return Task.FromResult(_envelopes.ContainsKey(messageId));
    }

    /// <summary>Exposes stored envelopes for testing assertions.</summary>
    public IReadOnlyCollection<MessageEnvelope> GetAllEnvelopes() => _envelopes.Values.ToList();
}
