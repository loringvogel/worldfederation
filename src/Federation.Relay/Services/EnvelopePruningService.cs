using Federation.Storage;

namespace Federation.Relay.Services;

/// <summary>
/// Background service that periodically prunes expired message envelopes from the store.
/// Runs every 10 minutes by default (configurable).
/// </summary>
public sealed class EnvelopePruningService : BackgroundService
{
    private readonly FederationDatabase _db;
    private readonly ILogger<EnvelopePruningService> _logger;
    private readonly TimeSpan _interval;

    public EnvelopePruningService(FederationDatabase db, ILogger<EnvelopePruningService> logger, TimeSpan? interval = null)
    {
        _db = db;
        _logger = logger;
        _interval = interval ?? TimeSpan.FromMinutes(10);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("EnvelopePruningService started with interval {Interval}.", _interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var pruned = _db.PruneExpiredEnvelopes();
                if (pruned > 0)
                {
                    _logger.LogInformation("Pruned {Count} expired envelopes.", pruned);
                }
            }
#pragma warning disable CA1031
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error pruning expired envelopes.");
            }
#pragma warning restore CA1031

            await Task.Delay(_interval, stoppingToken).ConfigureAwait(false);
        }
    }
}
