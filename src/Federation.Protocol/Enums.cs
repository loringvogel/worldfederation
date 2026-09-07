namespace Federation.Protocol;

/// <summary>Phases a discussion progresses through in the deliberation protocol.</summary>
public enum DiscussionPhase
{
    Draft,
    ProposalRound,
    CritiqueRound,
    RevisionRound,
    Vote,
    Synthesis,
    Closed
}

/// <summary>Type of message content within a discussion envelope.</summary>
public enum MessageType
{
    Proposal,
    Critique,
    Revision,
    Vote,
    Synthesis
}

/// <summary>Role a member holds within a council room.</summary>
public enum MemberRole
{
    Owner,
    Moderator,
    Participant,
    Synthesizer,
    Observer
}

/// <summary>Choices available during the voting phase.</summary>
public enum VoteChoice
{
    Approve,
    Reject,
    Abstain
}

/// <summary>Triggers that cause state transitions in the discussion state machine.</summary>
public enum DiscussionTrigger
{
    StartProposals,
    AllProposalsReceived,
    DeadlineExpired,
    StartCritiques,
    AllCritiquesReceived,
    StartRevisions,
    AllRevisionsReceived,
    StartVote,
    AllVotesReceived,
    StartSynthesis,
    SynthesisSubmitted,
    Cancel,
    Moderator
}
