using Federation.Protocol;
using Federation.Storage;
using Xunit;

namespace Federation.Security.Tests;

public sealed class EnvelopePruningTests : IDisposable
{
    private readonly FederationDatabase _db;

    public EnvelopePruningTests()
    {
        _db = new FederationDatabase(":memory:");
    }

    [Fact]
    public async Task PruneExpiredEnvelopes_RemovesExpiredOnly()
    {
        var store = new SqliteMessageStore(_db);
        var roomId = RoomId.New();
        var discussionId = DiscussionId.New();

        // Insert 2 expired envelopes
        var expired1 = CreateEnvelope(roomId, discussionId, DateTimeOffset.UtcNow.AddHours(-2));
        var expired2 = CreateEnvelope(roomId, discussionId, DateTimeOffset.UtcNow.AddHours(-1));
        // Insert 1 valid envelope
        var valid = CreateEnvelope(roomId, discussionId, DateTimeOffset.UtcNow.AddHours(24));

        await store.StoreEnvelopeAsync(expired1);
        await store.StoreEnvelopeAsync(expired2);
        await store.StoreEnvelopeAsync(valid);

        // Prune
        var pruned = _db.PruneExpiredEnvelopes();

        Assert.Equal(2, pruned);

        // Only the valid one remains
        var result = await store.GetEnvelopesAsync(roomId, discussionId, 0);
        Assert.Single(result.Envelopes);
        Assert.Equal(valid.MessageId, result.Envelopes[0].MessageId);
    }

    private static MessageEnvelope CreateEnvelope(RoomId roomId, DiscussionId discussionId, DateTimeOffset expiresAt)
    {
        return new MessageEnvelope
        {
            ProtocolVersion = "1.0",
            RoomId = roomId,
            DiscussionId = discussionId,
            Epoch = new EpochId(1),
            MessageId = MessageId.New(),
            SenderDeviceId = DeviceId.New(),
            SenderSequence = 1,
            MessageType = MessageType.Proposal,
            Round = 1,
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = expiresAt,
            CipherSuite = "AES-256-GCM+ECDSA-P256",
            Ciphertext = new byte[] { 0x01, 0x02, 0x03 },
            Signature = new byte[] { 0x04, 0x05, 0x06 },
        };
    }

    public void Dispose()
    {
        _db.Dispose();
    }
}
