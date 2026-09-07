using Federation.Protocol;
using Federation.Deliberation;
using Xunit;

namespace Federation.Deliberation.Tests;

public sealed class RoomPolicyTests
{
    [Fact]
    public void DefaultPolicy_HasExpectedDefaults()
    {
        var policy = new RoomPolicy();

        Assert.Equal(3, policy.MinParticipants);
        Assert.Equal(10, policy.MaxParticipants);
        Assert.Equal(2, policy.MaxRounds);
        Assert.Equal(65_536, policy.MaxMessageSizeBytes);
        Assert.Equal(100_000, policy.MaxTokenBudget);
        Assert.Equal(10.00m, policy.MaxMonetaryBudgetUsd);
    }

    [Fact]
    public void DefaultPolicy_Validates()
    {
        var policy = new RoomPolicy();
        var errors = policy.Validate();
        Assert.Empty(errors);
    }

    [Fact]
    public void InvalidPolicy_MinParticipantsTooLow_ReturnsError()
    {
        var policy = new RoomPolicy { MinParticipants = 1 };
        var errors = policy.Validate();
        Assert.Single(errors);
        Assert.Contains("MinParticipants", errors[0]);
    }

    [Fact]
    public void InvalidPolicy_MaxLessThanMin_ReturnsError()
    {
        var policy = new RoomPolicy { MinParticipants = 5, MaxParticipants = 3 };
        var errors = policy.Validate();
        Assert.Contains(errors, e => e.Contains("MaxParticipants"));
    }

    [Fact]
    public void GetDeadlineForPhase_ReturnsCorrectDurations()
    {
        var policy = new RoomPolicy();

        Assert.Equal(policy.ProposalDeadline, policy.GetDeadlineForPhase(DiscussionPhase.ProposalRound));
        Assert.Equal(policy.CritiqueDeadline, policy.GetDeadlineForPhase(DiscussionPhase.CritiqueRound));
        Assert.Equal(policy.RevisionDeadline, policy.GetDeadlineForPhase(DiscussionPhase.RevisionRound));
        Assert.Equal(policy.VoteDeadline, policy.GetDeadlineForPhase(DiscussionPhase.Vote));
        Assert.Equal(policy.SynthesisDeadline, policy.GetDeadlineForPhase(DiscussionPhase.Synthesis));
        Assert.Equal(TimeSpan.Zero, policy.GetDeadlineForPhase(DiscussionPhase.Draft));
        Assert.Equal(TimeSpan.Zero, policy.GetDeadlineForPhase(DiscussionPhase.Closed));
    }
}
