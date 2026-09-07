using Federation.Protocol;
using Federation.Deliberation;

namespace Federation.Relay.Services;

/// <summary>
/// Background service that monitors active discussions for deadline expiration.
/// When a deadline passes and not all submissions have been received, the service
/// fires a DeadlineExpired trigger to advance the discussion to the next phase.
/// </summary>
public sealed class RoundTimeoutService : BackgroundService
{
    private readonly IDiscussionRepository _discussions;
    private readonly IEventBus _eventBus;
    private readonly RoomPolicy _policy;
    private readonly ILogger<RoundTimeoutService> _logger;
    private readonly TimeSpan _pollInterval;

    public RoundTimeoutService(
        IDiscussionRepository discussions,
        IEventBus eventBus,
        ILogger<RoundTimeoutService> logger,
        TimeSpan? pollInterval = null)
    {
        _discussions = discussions;
        _eventBus = eventBus;
        _logger = logger;
        _policy = new RoomPolicy();
        _pollInterval = pollInterval ?? TimeSpan.FromSeconds(5);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("RoundTimeoutService started with poll interval {Interval}.", _pollInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckDeadlinesAsync(stoppingToken).ConfigureAwait(false);
            }
#pragma warning disable CA1031
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error checking deadlines.");
            }
#pragma warning restore CA1031

            await Task.Delay(_pollInterval, stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task CheckDeadlinesAsync(CancellationToken ct)
    {
        var activeDiscussions = await _discussions.GetActiveDiscussionsAsync(ct).ConfigureAwait(false);
        var now = DateTimeOffset.UtcNow;

        foreach (var discussion in activeDiscussions)
        {
            if (discussion.CurrentDeadline is null || discussion.CurrentDeadline > now)
                continue;

            if (!IsTimedPhase(discussion.Phase))
                continue;

            if (!DiscussionStateMachine.CanTransition(discussion.Phase, DiscussionTrigger.DeadlineExpired))
                continue;

            var previousPhase = discussion.Phase;
            var newPhase = DiscussionStateMachine.Transition(discussion.Phase, DiscussionTrigger.DeadlineExpired);
            var newDeadline = _policy.GetDeadlineForPhase(newPhase);
            DateTimeOffset? deadline = newDeadline > TimeSpan.Zero ? now.Add(newDeadline) : null;

            await _discussions.UpdateDiscussionStateAsync(
                discussion.DiscussionId, newPhase,
                discussion.CurrentRound + (newPhase != DiscussionPhase.Closed ? 1 : 0),
                deadline, ct).ConfigureAwait(false);

            await _eventBus.PublishAsync(new RoundAdvanced
            {
                RoomId = discussion.RoomId, DiscussionId = discussion.DiscussionId,
                PreviousPhase = previousPhase, NewPhase = newPhase,
                Trigger = DiscussionTrigger.DeadlineExpired,
            }, ct).ConfigureAwait(false);

            _logger.LogInformation("Discussion {DiscussionId} advanced from {Previous} to {New} due to deadline expiration.",
                discussion.DiscussionId, previousPhase, newPhase);
        }
    }

    private static bool IsTimedPhase(DiscussionPhase phase) => phase switch
    {
        DiscussionPhase.ProposalRound => true,
        DiscussionPhase.CritiqueRound => true,
        DiscussionPhase.RevisionRound => true,
        DiscussionPhase.Vote => true,
        DiscussionPhase.Synthesis => true,
        _ => false,
    };
}
