using System.Collections.Concurrent;
using Council.Contracts;

namespace Council.Relay.Api.Stores;

/// <summary>Thread-safe in-memory security event store for Phase 1 simulation.</summary>
public sealed class InMemorySecurityEventStore : ISecurityEventStore
{
    private readonly ConcurrentBag<SecurityEvent> _events = [];

    public Task AppendAsync(SecurityEvent securityEvent, CancellationToken ct = default)
    {
        _events.Add(securityEvent);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<SecurityEvent>> GetEventsAsync(RoomId? roomId, int limit = 100, CancellationToken ct = default)
    {
        IReadOnlyList<SecurityEvent> result = _events
            .Where(e => roomId is null || e.RoomId == roomId)
            .OrderByDescending(e => e.OccurredAt)
            .Take(limit)
            .ToList();
        return Task.FromResult(result);
    }
}
