using Federation.Deliberation;
using Federation.Protocol;
using Xunit;

namespace Federation.Security.Tests;

/// <summary>
/// Tests that submitting the wrong message type in the wrong phase is rejected by RoundValidator.
/// </summary>
public sealed class UnauthorizedMessageTypeTests
{
    private static readonly DeviceId TestDevice = DeviceId.New();

    [Fact]
    public void Synthesis_DuringProposalRound_IsRejected()
    {
        var submitted = new HashSet<DeviceId>();
        var errors = RoundValidator.Validate(
            DiscussionPhase.ProposalRound,
            MessageType.Synthesis,
            TestDevice,
            submitted,
            MemberRole.Synthesizer);

        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains("not allowed"));
    }

    [Fact]
    public void Proposal_DuringVotePhase_IsRejected()
    {
        var submitted = new HashSet<DeviceId>();
        var errors = RoundValidator.Validate(
            DiscussionPhase.Vote,
            MessageType.Proposal,
            TestDevice,
            submitted,
            MemberRole.Participant);

        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains("not allowed"));
    }

    [Fact]
    public void Critique_DuringRevisionRound_IsRejected()
    {
        var submitted = new HashSet<DeviceId>();
        var errors = RoundValidator.Validate(
            DiscussionPhase.RevisionRound,
            MessageType.Critique,
            TestDevice,
            submitted,
            MemberRole.Participant);

        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains("not allowed"));
    }

    [Fact]
    public void Vote_DuringDraftPhase_IsRejected()
    {
        var submitted = new HashSet<DeviceId>();
        var errors = RoundValidator.Validate(
            DiscussionPhase.Draft,
            MessageType.Vote,
            TestDevice,
            submitted,
            MemberRole.Participant);

        Assert.NotEmpty(errors);
    }

    [Fact]
    public void Revision_DuringCritiqueRound_IsRejected()
    {
        var submitted = new HashSet<DeviceId>();
        var errors = RoundValidator.Validate(
            DiscussionPhase.CritiqueRound,
            MessageType.Revision,
            TestDevice,
            submitted,
            MemberRole.Participant);

        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains("not allowed"));
    }

    [Fact]
    public void Proposal_DuringClosedPhase_IsRejected()
    {
        var submitted = new HashSet<DeviceId>();
        var errors = RoundValidator.Validate(
            DiscussionPhase.Closed,
            MessageType.Proposal,
            TestDevice,
            submitted,
            MemberRole.Participant);

        Assert.NotEmpty(errors);
    }
}
