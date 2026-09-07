namespace Council.Domain;

/// <summary>
/// Configurable policy limits for a council room.
/// Default values represent sensible Phase 1 defaults.
/// </summary>
public sealed record RoomPolicy
{
    public int MinParticipants { get; init; } = 3;
    public int MaxParticipants { get; init; } = 10;
    public int MaxRounds { get; init; } = 2;
    public int MaxMessageSizeBytes { get; init; } = 65_536;
    public TimeSpan ProposalDeadline { get; init; } = TimeSpan.FromMinutes(30);
    public TimeSpan CritiqueDeadline { get; init; } = TimeSpan.FromMinutes(20);
    public TimeSpan RevisionDeadline { get; init; } = TimeSpan.FromMinutes(20);
    public TimeSpan VoteDeadline { get; init; } = TimeSpan.FromMinutes(10);
    public TimeSpan SynthesisDeadline { get; init; } = TimeSpan.FromMinutes(15);
    public int MaxTokenBudget { get; init; } = 100_000;
    public decimal MaxMonetaryBudgetUsd { get; init; } = 10.00m;

    /// <summary>Returns the deadline duration for a given discussion phase.</summary>
    public TimeSpan GetDeadlineForPhase(Contracts.DiscussionPhase phase) => phase switch
    {
        Contracts.DiscussionPhase.ProposalRound => ProposalDeadline,
        Contracts.DiscussionPhase.CritiqueRound => CritiqueDeadline,
        Contracts.DiscussionPhase.RevisionRound => RevisionDeadline,
        Contracts.DiscussionPhase.Vote => VoteDeadline,
        Contracts.DiscussionPhase.Synthesis => SynthesisDeadline,
        _ => TimeSpan.Zero,
    };

    /// <summary>Validates that the policy values are internally consistent.</summary>
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (MinParticipants < 2)
            errors.Add("MinParticipants must be at least 2.");
        if (MaxParticipants < MinParticipants)
            errors.Add("MaxParticipants must be >= MinParticipants.");
        if (MaxRounds < 1)
            errors.Add("MaxRounds must be at least 1.");
        if (MaxMessageSizeBytes < 1024)
            errors.Add("MaxMessageSizeBytes must be at least 1024.");
        if (MaxTokenBudget <= 0)
            errors.Add("MaxTokenBudget must be positive.");
        if (MaxMonetaryBudgetUsd <= 0)
            errors.Add("MaxMonetaryBudgetUsd must be positive.");

        return errors;
    }
}
