using Council.Contracts;

namespace Council.Domain;

/// <summary>
/// Determines valid phase transitions for a council discussion.
/// The discussion follows: Draft -> ProposalRound -> CritiqueRound -> RevisionRound -> Vote -> Synthesis -> Closed.
/// Cancel is valid from any non-Closed phase. DeadlineExpired advances from timed phases.
/// </summary>
public static class DiscussionStateMachine
{
    private static readonly Dictionary<(DiscussionPhase Phase, DiscussionTrigger Trigger), DiscussionPhase> Transitions = new()
    {
        // Draft -> ProposalRound
        [(DiscussionPhase.Draft, DiscussionTrigger.StartProposals)] = DiscussionPhase.ProposalRound,

        // ProposalRound -> CritiqueRound
        [(DiscussionPhase.ProposalRound, DiscussionTrigger.AllProposalsReceived)] = DiscussionPhase.CritiqueRound,
        [(DiscussionPhase.ProposalRound, DiscussionTrigger.DeadlineExpired)] = DiscussionPhase.CritiqueRound,
        [(DiscussionPhase.ProposalRound, DiscussionTrigger.StartCritiques)] = DiscussionPhase.CritiqueRound,

        // CritiqueRound -> RevisionRound
        [(DiscussionPhase.CritiqueRound, DiscussionTrigger.AllCritiquesReceived)] = DiscussionPhase.RevisionRound,
        [(DiscussionPhase.CritiqueRound, DiscussionTrigger.DeadlineExpired)] = DiscussionPhase.RevisionRound,
        [(DiscussionPhase.CritiqueRound, DiscussionTrigger.StartRevisions)] = DiscussionPhase.RevisionRound,

        // RevisionRound -> Vote
        [(DiscussionPhase.RevisionRound, DiscussionTrigger.AllRevisionsReceived)] = DiscussionPhase.Vote,
        [(DiscussionPhase.RevisionRound, DiscussionTrigger.DeadlineExpired)] = DiscussionPhase.Vote,
        [(DiscussionPhase.RevisionRound, DiscussionTrigger.StartVote)] = DiscussionPhase.Vote,

        // Vote -> Synthesis
        [(DiscussionPhase.Vote, DiscussionTrigger.AllVotesReceived)] = DiscussionPhase.Synthesis,
        [(DiscussionPhase.Vote, DiscussionTrigger.DeadlineExpired)] = DiscussionPhase.Synthesis,
        [(DiscussionPhase.Vote, DiscussionTrigger.StartSynthesis)] = DiscussionPhase.Synthesis,

        // Synthesis -> Closed
        [(DiscussionPhase.Synthesis, DiscussionTrigger.SynthesisSubmitted)] = DiscussionPhase.Closed,
        [(DiscussionPhase.Synthesis, DiscussionTrigger.DeadlineExpired)] = DiscussionPhase.Closed,

        // Cancel from any active phase
        [(DiscussionPhase.Draft, DiscussionTrigger.Cancel)] = DiscussionPhase.Closed,
        [(DiscussionPhase.ProposalRound, DiscussionTrigger.Cancel)] = DiscussionPhase.Closed,
        [(DiscussionPhase.CritiqueRound, DiscussionTrigger.Cancel)] = DiscussionPhase.Closed,
        [(DiscussionPhase.RevisionRound, DiscussionTrigger.Cancel)] = DiscussionPhase.Closed,
        [(DiscussionPhase.Vote, DiscussionTrigger.Cancel)] = DiscussionPhase.Closed,
        [(DiscussionPhase.Synthesis, DiscussionTrigger.Cancel)] = DiscussionPhase.Closed,

        // Moderator override from any active phase advances to next
        [(DiscussionPhase.Draft, DiscussionTrigger.Moderator)] = DiscussionPhase.ProposalRound,
        [(DiscussionPhase.ProposalRound, DiscussionTrigger.Moderator)] = DiscussionPhase.CritiqueRound,
        [(DiscussionPhase.CritiqueRound, DiscussionTrigger.Moderator)] = DiscussionPhase.RevisionRound,
        [(DiscussionPhase.RevisionRound, DiscussionTrigger.Moderator)] = DiscussionPhase.Vote,
        [(DiscussionPhase.Vote, DiscussionTrigger.Moderator)] = DiscussionPhase.Synthesis,
        [(DiscussionPhase.Synthesis, DiscussionTrigger.Moderator)] = DiscussionPhase.Closed,
    };

    /// <summary>
    /// Attempts to transition from <paramref name="current"/> via <paramref name="trigger"/>.
    /// Returns the new phase on success.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the transition is not valid.</exception>
    public static DiscussionPhase Transition(DiscussionPhase current, DiscussionTrigger trigger)
    {
        if (current == DiscussionPhase.Closed)
        {
            throw new InvalidOperationException($"Cannot transition from Closed phase with trigger {trigger}.");
        }

        if (Transitions.TryGetValue((current, trigger), out var next))
        {
            return next;
        }

        throw new InvalidOperationException(
            $"Invalid transition: phase '{current}' does not accept trigger '{trigger}'.");
    }

    /// <summary>
    /// Checks whether a transition is valid without throwing.
    /// </summary>
    public static bool CanTransition(DiscussionPhase current, DiscussionTrigger trigger)
    {
        if (current == DiscussionPhase.Closed)
        {
            return false;
        }

        return Transitions.ContainsKey((current, trigger));
    }
}
