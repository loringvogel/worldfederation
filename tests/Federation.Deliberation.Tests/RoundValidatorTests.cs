using Federation.Protocol;
using Federation.Deliberation;
using Xunit;

namespace Federation.Deliberation.Tests;

public sealed class RoundValidatorTests
{
    private static readonly DeviceId Device1 = DeviceId.New();

    [Theory]
    [InlineData(DiscussionPhase.ProposalRound, MessageType.Proposal)]
    [InlineData(DiscussionPhase.CritiqueRound, MessageType.Critique)]
    [InlineData(DiscussionPhase.RevisionRound, MessageType.Revision)]
    [InlineData(DiscussionPhase.Vote, MessageType.Vote)]
    [InlineData(DiscussionPhase.Synthesis, MessageType.Synthesis)]
    public void CorrectPhase_CorrectMessageType_NoErrors(DiscussionPhase phase, MessageType messageType)
    {
        var submitted = new HashSet<DeviceId>();
        var role = messageType == MessageType.Synthesis ? MemberRole.Synthesizer : MemberRole.Participant;

        var errors = RoundValidator.Validate(phase, messageType, Device1, submitted, role);

        Assert.Empty(errors);
    }

    [Fact]
    public void WrongMessageType_ReturnsError()
    {
        var submitted = new HashSet<DeviceId>();
        var errors = RoundValidator.Validate(
            DiscussionPhase.ProposalRound,
            MessageType.Critique,
            Device1,
            submitted,
            MemberRole.Participant);

        Assert.Single(errors);
        Assert.Contains("not allowed", errors[0]);
    }

    [Fact]
    public void DuplicateSubmission_ReturnsError()
    {
        var submitted = new HashSet<DeviceId> { Device1 };
        var errors = RoundValidator.Validate(
            DiscussionPhase.ProposalRound,
            MessageType.Proposal,
            Device1,
            submitted,
            MemberRole.Participant);

        Assert.Single(errors);
        Assert.Contains("already submitted", errors[0]);
    }

    [Fact]
    public void Observer_CannotSubmit()
    {
        var submitted = new HashSet<DeviceId>();
        var errors = RoundValidator.Validate(
            DiscussionPhase.ProposalRound,
            MessageType.Proposal,
            Device1,
            submitted,
            MemberRole.Observer);

        Assert.Single(errors);
        Assert.Contains("Observers", errors[0]);
    }

    [Fact]
    public void Draft_Phase_RejectsAllMessages()
    {
        var submitted = new HashSet<DeviceId>();
        var errors = RoundValidator.Validate(
            DiscussionPhase.Draft,
            MessageType.Proposal,
            Device1,
            submitted,
            MemberRole.Participant);

        Assert.NotEmpty(errors);
        Assert.Contains("No messages are accepted", errors[0]);
    }

    [Fact]
    public void Closed_Phase_RejectsAllMessages()
    {
        var submitted = new HashSet<DeviceId>();
        var errors = RoundValidator.Validate(
            DiscussionPhase.Closed,
            MessageType.Proposal,
            Device1,
            submitted,
            MemberRole.Participant);

        Assert.NotEmpty(errors);
    }

    [Fact]
    public void Synthesis_NonSynthesizer_ReturnsError()
    {
        var submitted = new HashSet<DeviceId>();
        var errors = RoundValidator.Validate(
            DiscussionPhase.Synthesis,
            MessageType.Synthesis,
            Device1,
            submitted,
            MemberRole.Participant);

        Assert.Single(errors);
        Assert.Contains("Synthesizer", errors[0]);
    }

    [Fact]
    public void Synthesis_Owner_Allowed()
    {
        var submitted = new HashSet<DeviceId>();
        var errors = RoundValidator.Validate(
            DiscussionPhase.Synthesis,
            MessageType.Synthesis,
            Device1,
            submitted,
            MemberRole.Owner);

        Assert.Empty(errors);
    }

    [Fact]
    public void GetExpectedMessageType_ReturnsCorrectTypes()
    {
        Assert.Equal(MessageType.Proposal, RoundValidator.GetExpectedMessageType(DiscussionPhase.ProposalRound));
        Assert.Equal(MessageType.Critique, RoundValidator.GetExpectedMessageType(DiscussionPhase.CritiqueRound));
        Assert.Equal(MessageType.Revision, RoundValidator.GetExpectedMessageType(DiscussionPhase.RevisionRound));
        Assert.Equal(MessageType.Vote, RoundValidator.GetExpectedMessageType(DiscussionPhase.Vote));
        Assert.Equal(MessageType.Synthesis, RoundValidator.GetExpectedMessageType(DiscussionPhase.Synthesis));
        Assert.Null(RoundValidator.GetExpectedMessageType(DiscussionPhase.Draft));
        Assert.Null(RoundValidator.GetExpectedMessageType(DiscussionPhase.Closed));
    }
}
