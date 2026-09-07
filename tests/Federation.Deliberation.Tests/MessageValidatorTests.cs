using Federation.Protocol;
using Federation.Deliberation;
using Xunit;

namespace Federation.Deliberation.Tests;

public sealed class MessageValidatorTests
{
    private static MessageEnvelope CreateValidEnvelope(EpochId epoch)
    {
        return new MessageEnvelope
        {
            ProtocolVersion = "1.0",
            RoomId = RoomId.New(),
            DiscussionId = DiscussionId.New(),
            Epoch = epoch,
            MessageId = MessageId.New(),
            SenderDeviceId = DeviceId.New(),
            SenderSequence = 1,
            MessageType = MessageType.Proposal,
            Round = 1,
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1),
            CipherSuite = "AES-256-GCM+ECDSA-P256",
            Ciphertext = new byte[100],
            Signature = new byte[64],
        };
    }

    [Fact]
    public void ValidEnvelope_NoErrors()
    {
        var epoch = new EpochId(1);
        var envelope = CreateValidEnvelope(epoch);
        var errors = MessageValidator.Validate(envelope, new RoomPolicy(), epoch, DateTimeOffset.UtcNow);
        Assert.Empty(errors);
    }

    [Fact]
    public void EpochMismatch_ReturnsError()
    {
        var envelope = CreateValidEnvelope(new EpochId(1));
        var errors = MessageValidator.Validate(envelope, new RoomPolicy(), new EpochId(2), DateTimeOffset.UtcNow);
        Assert.Contains(errors, e => e.Contains("Epoch mismatch"));
    }

    [Fact]
    public void ExpiredEnvelope_ReturnsError()
    {
        var epoch = new EpochId(1);
        var envelope = CreateValidEnvelope(epoch) with { ExpiresAt = DateTimeOffset.UtcNow.AddHours(-1) };
        var errors = MessageValidator.Validate(envelope, new RoomPolicy(), epoch, DateTimeOffset.UtcNow);
        Assert.Contains(errors, e => e.Contains("expired"));
    }

    [Fact]
    public void OversizedCiphertext_ReturnsError()
    {
        var epoch = new EpochId(1);
        var envelope = CreateValidEnvelope(epoch) with { Ciphertext = new byte[100_000] };
        var errors = MessageValidator.Validate(envelope, new RoomPolicy(), epoch, DateTimeOffset.UtcNow);
        Assert.Contains(errors, e => e.Contains("exceeds maximum"));
    }

    [Fact]
    public void EmptyCiphertext_ReturnsError()
    {
        var epoch = new EpochId(1);
        var envelope = CreateValidEnvelope(epoch) with { Ciphertext = [] };
        var errors = MessageValidator.Validate(envelope, new RoomPolicy(), epoch, DateTimeOffset.UtcNow);
        Assert.Contains(errors, e => e.Contains("must not be empty"));
    }

    [Fact]
    public void EmptySignature_ReturnsError()
    {
        var epoch = new EpochId(1);
        var envelope = CreateValidEnvelope(epoch) with { Signature = [] };
        var errors = MessageValidator.Validate(envelope, new RoomPolicy(), epoch, DateTimeOffset.UtcNow);
        Assert.Contains(errors, e => e.Contains("Signature"));
    }

    [Fact]
    public void NegativeSequence_ReturnsError()
    {
        var epoch = new EpochId(1);
        var envelope = CreateValidEnvelope(epoch) with { SenderSequence = -1 };
        var errors = MessageValidator.Validate(envelope, new RoomPolicy(), epoch, DateTimeOffset.UtcNow);
        Assert.Contains(errors, e => e.Contains("non-negative"));
    }
}
