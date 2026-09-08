using Federation.Protocol;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Federation.Sync;

/// <summary>
/// Hosted service that synchronizes federation events between peers.
/// Scoped to only sync events for rooms the local node is a member of.
/// A node never pulls events for rooms it doesn't belong to -- it cannot map the federation's room graph.
/// </summary>
public sealed class SyncEngine : IHostedService
{
    private readonly IEventLog _localLog;
    private readonly ILogger<SyncEngine> _logger;
    private readonly List<FederationEvent> _quarantined = [];
    private readonly object _lock = new();
    private readonly IEnumerable<RoomId> _memberRooms;

    public SyncEngine(IEventLog localLog, ILogger<SyncEngine> logger)
        : this(localLog, logger, Enumerable.Empty<RoomId>())
    {
    }

    public SyncEngine(IEventLog localLog, ILogger<SyncEngine> logger, IEnumerable<RoomId> memberRooms)
    {
        ArgumentNullException.ThrowIfNull(localLog);
        _localLog = localLog;
        _logger = logger;
        _memberRooms = memberRooms ?? Enumerable.Empty<RoomId>();
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
    /// Only fetches events for rooms the local node is a member of.
    /// </summary>
    public async Task SyncFromAsync(IEventLog remoteLog, string? afterEventId, Func<FederationEvent, bool> validateSignature, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(remoteLog);
        ArgumentNullException.ThrowIfNull(validateSignature);

        var roomList = _memberRooms.ToList();
        var remoteEvents = roomList.Count > 0
            ? await remoteLog.GetEventsForRoomsAsync(roomList, afterEventId, ct).ConfigureAwait(false)
            : await remoteLog.GetEventsAsync(afterEventId, ct).ConfigureAwait(false);

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
