using Federation.Protocol;
using Federation.Storage;
using Xunit;

namespace Federation.Security.Tests;

public sealed class CursorTests : IDisposable
{
    private readonly FederationDatabase _db;

    public CursorTests()
    {
        _db = new FederationDatabase(":memory:");
    }

    [Fact]
    public async Task GetEnvelopes_RespectsAfterCursor()
    {
        var store = new SqliteMessageStore(_db);
        var roomId = RoomId.New();
        var discussionId = DiscussionId.New();

        var env1 = CreateEnvelope(roomId, discussionId);
        var env2 = CreateEnvelope(roomId, discussionId);
        var env3 = CreateEnvelope(roomId, discussionId);

        var cursor1 = await store.StoreEnvelopeAsync(env1);
        var cursor2 = await store.StoreEnvelopeAsync(env2);
        var cursor3 = await store.StoreEnvelopeAsync(env3);

        // Verify cursors are monotonically increasing
        Assert.True(cursor1 > 0);
        Assert.True(cursor2 > cursor1);
        Assert.True(cursor3 > cursor2);

        // Get envelopes after the first cursor -- should return only 2nd and 3rd
        var result = await store.GetEnvelopesAsync(roomId, discussionId, cursor1);

        Assert.Equal(2, result.Envelopes.Count);
        Assert.Equal(env2.MessageId, result.Envelopes[0].MessageId);
        Assert.Equal(env3.MessageId, result.Envelopes[1].MessageId);
        Assert.Equal(cursor3, result.Cursor);
    }

    private static MessageEnvelope CreateEnvelope(RoomId roomId, DiscussionId discussionId)
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
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(24),
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
