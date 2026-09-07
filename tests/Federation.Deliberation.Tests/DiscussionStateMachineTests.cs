using Federation.Protocol;
using Federation.Deliberation;
using Xunit;

namespace Federation.Deliberation.Tests;

public sealed class DiscussionStateMachineTests
{
    [Fact]
    public void Draft_StartProposals_TransitionsToProposalRound()
    {
        var result = DiscussionStateMachine.Transition(DiscussionPhase.Draft, DiscussionTrigger.StartProposals);
        Assert.Equal(DiscussionPhase.ProposalRound, result);
    }

    [Fact]
    public void ProposalRound_AllProposalsReceived_TransitionsToCritiqueRound()
    {
        var result = DiscussionStateMachine.Transition(DiscussionPhase.ProposalRound, DiscussionTrigger.AllProposalsReceived);
        Assert.Equal(DiscussionPhase.CritiqueRound, result);
    }

    [Fact]
    public void ProposalRound_DeadlineExpired_TransitionsToCritiqueRound()
    {
        var result = DiscussionStateMachine.Transition(DiscussionPhase.ProposalRound, DiscussionTrigger.DeadlineExpired);
        Assert.Equal(DiscussionPhase.CritiqueRound, result);
    }

    [Fact]
    public void CritiqueRound_AllCritiquesReceived_TransitionsToRevisionRound()
    {
        var result = DiscussionStateMachine.Transition(DiscussionPhase.CritiqueRound, DiscussionTrigger.AllCritiquesReceived);
        Assert.Equal(DiscussionPhase.RevisionRound, result);
    }

    [Fact]
    public void RevisionRound_AllRevisionsReceived_TransitionsToVote()
    {
        var result = DiscussionStateMachine.Transition(DiscussionPhase.RevisionRound, DiscussionTrigger.AllRevisionsReceived);
        Assert.Equal(DiscussionPhase.Vote, result);
    }

    [Fact]
    public void Vote_AllVotesReceived_TransitionsToSynthesis()
    {
        var result = DiscussionStateMachine.Transition(DiscussionPhase.Vote, DiscussionTrigger.AllVotesReceived);
        Assert.Equal(DiscussionPhase.Synthesis, result);
    }

    [Fact]
    public void Synthesis_SynthesisSubmitted_TransitionsToClosed()
    {
        var result = DiscussionStateMachine.Transition(DiscussionPhase.Synthesis, DiscussionTrigger.SynthesisSubmitted);
        Assert.Equal(DiscussionPhase.Closed, result);
    }

    [Theory]
    [InlineData(DiscussionPhase.Draft)]
    [InlineData(DiscussionPhase.ProposalRound)]
    [InlineData(DiscussionPhase.CritiqueRound)]
    [InlineData(DiscussionPhase.RevisionRound)]
    [InlineData(DiscussionPhase.Vote)]
    [InlineData(DiscussionPhase.Synthesis)]
    public void Cancel_FromAnyActivePhase_TransitionsToClosed(DiscussionPhase phase)
    {
        var result = DiscussionStateMachine.Transition(phase, DiscussionTrigger.Cancel);
        Assert.Equal(DiscussionPhase.Closed, result);
    }

    [Theory]
    [InlineData(DiscussionPhase.ProposalRound)]
    [InlineData(DiscussionPhase.CritiqueRound)]
    [InlineData(DiscussionPhase.RevisionRound)]
    [InlineData(DiscussionPhase.Vote)]
    [InlineData(DiscussionPhase.Synthesis)]
    public void DeadlineExpired_FromTimedPhases_AdvancesToNextPhase(DiscussionPhase phase)
    {
        var result = DiscussionStateMachine.Transition(phase, DiscussionTrigger.DeadlineExpired);
        Assert.NotEqual(phase, result);
    }

    [Fact]
    public void Closed_AnyTrigger_ThrowsInvalidOperation()
    {
        foreach (var trigger in Enum.GetValues<DiscussionTrigger>())
        {
            Assert.Throws<InvalidOperationException>(() =>
                DiscussionStateMachine.Transition(DiscussionPhase.Closed, trigger));
        }
    }

    [Fact]
    public void Draft_AllProposalsReceived_ThrowsInvalidOperation()
    {
        Assert.Throws<InvalidOperationException>(() =>
            DiscussionStateMachine.Transition(DiscussionPhase.Draft, DiscussionTrigger.AllProposalsReceived));
    }

    [Fact]
    public void Draft_DeadlineExpired_ThrowsInvalidOperation()
    {
        Assert.Throws<InvalidOperationException>(() =>
            DiscussionStateMachine.Transition(DiscussionPhase.Draft, DiscussionTrigger.DeadlineExpired));
    }

    [Fact]
    public void ProposalRound_SynthesisSubmitted_ThrowsInvalidOperation()
    {
        Assert.Throws<InvalidOperationException>(() =>
            DiscussionStateMachine.Transition(DiscussionPhase.ProposalRound, DiscussionTrigger.SynthesisSubmitted));
    }

    [Theory]
    [InlineData(DiscussionPhase.Draft, DiscussionTrigger.Moderator, DiscussionPhase.ProposalRound)]
    [InlineData(DiscussionPhase.ProposalRound, DiscussionTrigger.Moderator, DiscussionPhase.CritiqueRound)]
    [InlineData(DiscussionPhase.CritiqueRound, DiscussionTrigger.Moderator, DiscussionPhase.RevisionRound)]
    [InlineData(DiscussionPhase.RevisionRound, DiscussionTrigger.Moderator, DiscussionPhase.Vote)]
    [InlineData(DiscussionPhase.Vote, DiscussionTrigger.Moderator, DiscussionPhase.Synthesis)]
    [InlineData(DiscussionPhase.Synthesis, DiscussionTrigger.Moderator, DiscussionPhase.Closed)]
    public void Moderator_FromAnyActivePhase_AdvancesToNextPhase(DiscussionPhase from, DiscussionTrigger trigger, DiscussionPhase expected)
    {
        var result = DiscussionStateMachine.Transition(from, trigger);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void CanTransition_ValidTransition_ReturnsTrue()
    {
        Assert.True(DiscussionStateMachine.CanTransition(DiscussionPhase.Draft, DiscussionTrigger.StartProposals));
    }

    [Fact]
    public void CanTransition_InvalidTransition_ReturnsFalse()
    {
        Assert.False(DiscussionStateMachine.CanTransition(DiscussionPhase.Draft, DiscussionTrigger.AllProposalsReceived));
    }

    [Fact]
    public void CanTransition_FromClosed_AlwaysReturnsFalse()
    {
        foreach (var trigger in Enum.GetValues<DiscussionTrigger>())
        {
            Assert.False(DiscussionStateMachine.CanTransition(DiscussionPhase.Closed, trigger));
        }
    }

    [Fact]
    public void FullHappyPath_DraftToClosed()
    {
        var phase = DiscussionPhase.Draft;
        phase = DiscussionStateMachine.Transition(phase, DiscussionTrigger.StartProposals);
        Assert.Equal(DiscussionPhase.ProposalRound, phase);

        phase = DiscussionStateMachine.Transition(phase, DiscussionTrigger.AllProposalsReceived);
        Assert.Equal(DiscussionPhase.CritiqueRound, phase);

        phase = DiscussionStateMachine.Transition(phase, DiscussionTrigger.AllCritiquesReceived);
        Assert.Equal(DiscussionPhase.RevisionRound, phase);

        phase = DiscussionStateMachine.Transition(phase, DiscussionTrigger.AllRevisionsReceived);
        Assert.Equal(DiscussionPhase.Vote, phase);

        phase = DiscussionStateMachine.Transition(phase, DiscussionTrigger.AllVotesReceived);
        Assert.Equal(DiscussionPhase.Synthesis, phase);

        phase = DiscussionStateMachine.Transition(phase, DiscussionTrigger.SynthesisSubmitted);
        Assert.Equal(DiscussionPhase.Closed, phase);
    }
}
