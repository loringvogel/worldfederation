using Federation.Protocol;

namespace Federation.Storage;

/// <summary>SQLite-backed local message cache. Content is stored re-encrypted with the device key.</summary>
public sealed class SqliteLocalMessageCache : ILocalMessageCache
{
    private readonly FederationDatabase _db;

    public SqliteLocalMessageCache(FederationDatabase db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    public Task StoreAsync(MessageId messageId, RoomId roomId, DiscussionId discussionId,
        DeviceId senderDeviceId, MessageType type, int round,
        byte[] contentEncrypted, CancellationToken ct = default)
    {
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = """
            INSERT OR IGNORE INTO local_message_cache
            (message_id, room_id, discussion_id, sender_device_id, message_type, round, content_encrypted, received_at)
            VALUES (@mid, @rid, @did, @sender, @type, @round, @content, @received)
            """;
        cmd.Parameters.AddWithValue("@mid", messageId.Value.ToString());
        cmd.Parameters.AddWithValue("@rid", roomId.Value.ToString());
        cmd.Parameters.AddWithValue("@did", discussionId.Value.ToString());
        cmd.Parameters.AddWithValue("@sender", senderDeviceId.Value.ToString());
        cmd.Parameters.AddWithValue("@type", type.ToString());
        cmd.Parameters.AddWithValue("@round", round);
        cmd.Parameters.AddWithValue("@content", contentEncrypted);
        cmd.Parameters.AddWithValue("@received", DateTimeOffset.UtcNow.ToString("O"));
        cmd.ExecuteNonQuery();
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<CachedMessage>> GetMessagesAsync(RoomId roomId, DiscussionId discussionId, CancellationToken ct = default)
    {
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = """
            SELECT message_id, sender_device_id, message_type, round, content_encrypted, received_at
            FROM local_message_cache
            WHERE room_id = @rid AND discussion_id = @did
            ORDER BY rowid
            """;
        cmd.Parameters.AddWithValue("@rid", roomId.Value.ToString());
        cmd.Parameters.AddWithValue("@did", discussionId.Value.ToString());

        var list = new List<CachedMessage>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new CachedMessage
            {
                MessageId = new MessageId(Guid.Parse(reader.GetString(0))),
                SenderDeviceId = new DeviceId(Guid.Parse(reader.GetString(1))),
                Type = Enum.Parse<MessageType>(reader.GetString(2)),
                Round = reader.GetInt32(3),
                ContentEncrypted = (byte[])reader.GetValue(4),
                ReceivedAt = DateTimeOffset.Parse(reader.GetString(5), System.Globalization.CultureInfo.InvariantCulture),
            });
        }
        return Task.FromResult<IReadOnlyList<CachedMessage>>(list);
    }

    public Task WipeAsync(CancellationToken ct = default)
    {
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = "DELETE FROM local_message_cache";
        cmd.ExecuteNonQuery();
        return Task.CompletedTask;
    }
}
