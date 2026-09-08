using System.Collections.Concurrent;
using Federation.Protocol;

namespace Federation.Sync;

/// <summary>In-memory event log for development and testing.</summary>
public sealed class InMemoryEventLog : IEventLog
{
    private readonly ConcurrentDictionary<string, FederationEvent> _events = new();
    private readonly List<string> _orderedIds = [];
    private readonly object _lock = new();

    public Task AppendAsync(FederationEvent federationEvent, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(federationEvent);
        var id = federationEvent.EventId.ToString();

        if (_events.TryAdd(id, federationEvent))
        {
            lock (_lock)
            {
                _orderedIds.Add(id);
            }
        }
        // Idempotent: duplicate appends are silently ignored

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<FederationEvent>> GetEventsAsync(string? afterEventId, CancellationToken ct = default)
    {
        lock (_lock)
        {
            int startIndex = 0;
            if (afterEventId is not null)
            {
                var idx = _orderedIds.IndexOf(afterEventId);
                if (idx >= 0)
                {
                    startIndex = idx + 1;
                }
            }

            var result = new List<FederationEvent>();
            for (int i = startIndex; i < _orderedIds.Count; i++)
            {
                if (_events.TryGetValue(_orderedIds[i], out var evt))
                {
                    result.Add(evt);
                }
            }

            return Task.FromResult<IReadOnlyList<FederationEvent>>(result);
        }
    }

    public Task<FederationEvent?> GetEventAsync(string eventId, CancellationToken ct = default)
    {
        _events.TryGetValue(eventId, out var evt);
        return Task.FromResult(evt);
    }

    public Task<IReadOnlyList<FederationEvent>> GetEventsForRoomsAsync(IEnumerable<RoomId> roomIds, string? afterEventId, CancellationToken ct = default)
    {
        // In-memory: filter events by room if they carry room info, otherwise fall back to all events
        var roomSet = new HashSet<RoomId>(roomIds);

        lock (_lock)
        {
            int startIndex = 0;
            if (afterEventId is not null)
            {
                var idx = _orderedIds.IndexOf(afterEventId);
                if (idx >= 0)
                {
                    startIndex = idx + 1;
                }
            }

            var result = new List<FederationEvent>();
            for (int i = startIndex; i < _orderedIds.Count; i++)
            {
                if (_events.TryGetValue(_orderedIds[i], out var evt))
                {
                    // Filter by room ID if the event contains one
                    var roomId = ExtractRoomId(evt);
                    if (roomId is null || roomSet.Contains(roomId.Value))
                    {
                        result.Add(evt);
                    }
                }
            }

            return Task.FromResult<IReadOnlyList<FederationEvent>>(result);
        }
    }

    private static RoomId? ExtractRoomId(FederationEvent evt)
    {
        return evt switch
        {
            MessageAcceptedEvent e => e.Payload.RoomId,
            RoundAdvancedEvent e => e.Payload.RoomId,
            MembershipChangedEvent e => e.Payload.RoomId,
            DiscussionClosedEvent e => e.Payload.RoomId,
            _ => null,
        };
    }
}
