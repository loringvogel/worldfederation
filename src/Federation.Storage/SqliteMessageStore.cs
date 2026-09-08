using Federation.Protocol;
using Microsoft.Data.Sqlite;

namespace Federation.Storage;

/// <summary>SQLite-backed message store. Stores only ciphertext; never stores plaintext content.</summary>
public sealed class SqliteMessageStore : IMessageStore
{
    private readonly FederationDatabase _db;

    public SqliteMessageStore(FederationDatabase db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    public Task<long> StoreEnvelopeAsync(MessageEnvelope envelope, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = """
            INSERT OR IGNORE INTO message_envelopes
            (message_id, room_id, discussion_id, epoch, sender_device_id, message_type, round, created_at, expires_at, cipher_suite, ciphertext, signature, inserted_at)
            VALUES (@mid, @rid, @did, @epoch, @sender, @mtype, @round, @created, @expires, @suite, @cipher, @sig, @inserted);
            SELECT last_insert_rowid()
            """;
        cmd.Parameters.AddWithValue("@mid", envelope.MessageId.Value.ToString());
        cmd.Parameters.AddWithValue("@rid", envelope.RoomId.Value.ToString());
        cmd.Parameters.AddWithValue("@did", envelope.DiscussionId.Value.ToString());
        cmd.Parameters.AddWithValue("@epoch", envelope.Epoch.Value);
        cmd.Parameters.AddWithValue("@sender", envelope.SenderDeviceId.Value.ToString());
        cmd.Parameters.AddWithValue("@mtype", envelope.MessageType.ToString());
        cmd.Parameters.AddWithValue("@round", envelope.Round);
        cmd.Parameters.AddWithValue("@created", envelope.CreatedAt.ToString("O"));
        cmd.Parameters.AddWithValue("@expires", envelope.ExpiresAt.ToString("O"));
        cmd.Parameters.AddWithValue("@suite", envelope.CipherSuite);
        cmd.Parameters.AddWithValue("@cipher", envelope.Ciphertext);
        cmd.Parameters.AddWithValue("@sig", envelope.Signature);
        cmd.Parameters.AddWithValue("@inserted", DateTimeOffset.UtcNow.ToString("O"));

        var rowid = (long)(cmd.ExecuteScalar() ?? 0);
        return Task.FromResult(rowid);
    }

    public Task<GetEnvelopesResponse> GetEnvelopesAsync(RoomId roomId, DiscussionId discussionId, long afterCursor, CancellationToken ct = default)
    {
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = """
            SELECT message_id, room_id, discussion_id, epoch, sender_device_id, message_type, round, created_at, expires_at, cipher_suite, ciphertext, signature, rowid
            FROM message_envelopes
            WHERE room_id = @rid AND discussion_id = @did AND rowid > @afterCursor
            ORDER BY rowid
            """;
        cmd.Parameters.AddWithValue("@rid", roomId.Value.ToString());
        cmd.Parameters.AddWithValue("@did", discussionId.Value.ToString());
        cmd.Parameters.AddWithValue("@afterCursor", afterCursor);

        var envelopes = new List<MessageEnvelope>();
        long maxRowid = afterCursor;
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            envelopes.Add(ReadEnvelope(reader));
            var rowid = reader.GetInt64(12);
            if (rowid > maxRowid) maxRowid = rowid;
        }

        return Task.FromResult(new GetEnvelopesResponse { Envelopes = envelopes, Cursor = maxRowid });
    }

    public Task<bool> ExistsAsync(MessageId messageId, CancellationToken ct = default)
    {
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(1) FROM message_envelopes WHERE message_id = @mid";
        cmd.Parameters.AddWithValue("@mid", messageId.Value.ToString());
        var count = (long)(cmd.ExecuteScalar() ?? 0);
        return Task.FromResult(count > 0);
    }

    private static MessageEnvelope ReadEnvelope(SqliteDataReader reader)
    {
        return new MessageEnvelope
        {
            ProtocolVersion = "1.0",
            MessageId = new MessageId(Guid.Parse(reader.GetString(0))),
            RoomId = new RoomId(Guid.Parse(reader.GetString(1))),
            DiscussionId = new DiscussionId(Guid.Parse(reader.GetString(2))),
            Epoch = new EpochId(reader.GetInt64(3)),
            SenderDeviceId = new DeviceId(Guid.Parse(reader.GetString(4))),
            MessageType = Enum.Parse<MessageType>(reader.GetString(5)),
            Round = reader.GetInt32(6),
            CreatedAt = DateTimeOffset.Parse(reader.GetString(7), System.Globalization.CultureInfo.InvariantCulture),
            ExpiresAt = DateTimeOffset.Parse(reader.GetString(8), System.Globalization.CultureInfo.InvariantCulture),
            CipherSuite = reader.GetString(9),
            Ciphertext = (byte[])reader.GetValue(10),
            Signature = (byte[])reader.GetValue(11),
            SenderSequence = 0,
        };
    }
}
