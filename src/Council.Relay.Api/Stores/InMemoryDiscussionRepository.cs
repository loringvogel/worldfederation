using System.Collections.Concurrent;
using Council.Contracts;

namespace Council.Relay.Api.Stores;

/// <summary>Thread-safe in-memory discussion repository for Phase 1 simulation.</summary>
public sealed class InMemoryDiscussionRepository : IDiscussionRepository
{
    private readonly ConcurrentDictionary<DiscussionId, DiscussionState> _discussions = new();

    public Task<DiscussionState> CreateDiscussionAsync(CreateDiscussionRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var discussionId = DiscussionId.New();
        var state = new DiscussionState
        {
            DiscussionId = discussionId,
            RoomId = request.RoomId,
            Topic = request.Topic,
            Phase = DiscussionPhase.Draft,
            CurrentRound = 0,
            CreatedAt = DateTimeOffset.UtcNow,
            TotalSubmissions = 0,
            ExpectedSubmissions = 0,
        };

        if (!_discussions.TryAdd(discussionId, state))
        {
            throw new InvalidOperationException("Failed to create discussion (ID collision).");
        }

        return Task.FromResult(state);
    }

    public Task<DiscussionState?> GetDiscussionAsync(DiscussionId discussionId, CancellationToken ct = default)
    {
        _discussions.TryGetValue(discussionId, out var state);
        return Task.FromResult(state);
    }

    public Task UpdateDiscussionStateAsync(DiscussionId discussionId, DiscussionPhase newPhase, int newRound, DateTimeOffset? newDeadline, CancellationToken ct = default)
    {
        if (!_discussions.TryGetValue(discussionId, out var existing))
        {
            throw new InvalidOperationException($"Discussion '{discussionId}' not found.");
        }

        _discussions[discussionId] = existing with
        {
            Phase = newPhase,
            CurrentRound = newRound,
            CurrentDeadline = newDeadline,
            TotalSubmissions = 0, // Reset submission count for the new round
        };

        return Task.CompletedTask;
    }

    public Task IncrementSubmissionCountAsync(DiscussionId discussionId, CancellationToken ct = default)
    {
        if (!_discussions.TryGetValue(discussionId, out var existing))
        {
            throw new InvalidOperationException($"Discussion '{discussionId}' not found.");
        }

        _discussions[discussionId] = existing with { TotalSubmissions = existing.TotalSubmissions + 1 };
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<DiscussionState>> GetActiveDiscussionsAsync(CancellationToken ct = default)
    {
        IReadOnlyList<DiscussionState> result = _discussions.Values
            .Where(d => d.Phase != DiscussionPhase.Closed)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<DiscussionState>> GetDiscussionsInRoomAsync(RoomId roomId, CancellationToken ct = default)
    {
        IReadOnlyList<DiscussionState> result = _discussions.Values
            .Where(d => d.RoomId == roomId)
            .ToList();
        return Task.FromResult(result);
    }
}
