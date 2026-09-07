using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Federation.Sync;

/// <summary>
/// Hosted service that synchronizes federation events between peers.
/// On startup, fetches events from connected peers that are newer than our latest known event ID,
/// validates signatures, appends valid events, and quarantines invalid ones.
/// Phase 3 will replace polling with a push-based sync over Federation.Transport.
/// </summary>
public sealed class SyncEngine : IHostedService
{
    private readonly IEventLog _localLog;
    private readonly ILogger<SyncEngine> _logger;
    private readonly List<FederationEvent> _quarantined = [];
    private readonly object _lock = new();

    public SyncEngine(IEventLog localLog, ILogger<SyncEngine> logger)
    {
        ArgumentNullException.ThrowIfNull(localLog);
        _localLog = localLog;
        _logger = logger;
    }

    /// <summary>Events that failed signature validation.</summary>
    public IReadOnlyList<FederationEvent> QuarantinedEvents
    {
        get
        {
            lock (_lock)
            {
                return _quarantined.ToList();
            }
        }
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("SyncEngine started.");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("SyncEngine stopped.");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Pulls events from a remote event log and appends valid ones to the local log.
    /// Events with invalid signatures are quarantined.
    /// </summary>
    public async Task SyncFromAsync(IEventLog remoteLog, string? afterEventId, Func<FederationEvent, bool> validateSignature, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(remoteLog);
        ArgumentNullException.ThrowIfNull(validateSignature);

        var remoteEvents = await remoteLog.GetEventsAsync(afterEventId, ct).ConfigureAwait(false);

        foreach (var evt in remoteEvents)
        {
            if (validateSignature(evt))
            {
                await _localLog.AppendAsync(evt, ct).ConfigureAwait(false);
            }
            else
            {
                lock (_lock)
                {
                    _quarantined.Add(evt);
                }
                _logger.LogWarning("Quarantined event {EventId} with type {EventType} due to invalid signature.",
                    evt.EventId, evt.EventType);
            }
        }
    }
}
