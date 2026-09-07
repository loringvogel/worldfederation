namespace Federation.Sync;

/// <summary>Append-only event log for federation sync.</summary>
public interface IEventLog
{
    Task AppendAsync(FederationEvent federationEvent, CancellationToken ct = default);
    Task<IReadOnlyList<FederationEvent>> GetEventsAsync(string? afterEventId, CancellationToken ct = default);
    Task<FederationEvent?> GetEventAsync(string eventId, CancellationToken ct = default);
}
