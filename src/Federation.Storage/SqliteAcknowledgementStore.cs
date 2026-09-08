using Federation.Protocol;

namespace Federation.Storage;

/// <summary>SQLite-backed acknowledgement cursor store.</summary>
public sealed class SqliteAcknowledgementStore : IAcknowledgementStore
{
    private readonly FederationDatabase _db;

    public SqliteAcknowledgementStore(FederationDatabase db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    public Task<long> GetCursorAsync(RoomId roomId, DiscussionId discussionId, CancellationToken ct = default)
    {
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = "SELECT cursor FROM acknowledgements WHERE room_id = @rid AND discussion_id = @did";
        cmd.Parameters.AddWithValue("@rid", roomId.Value.ToString());
        cmd.Parameters.AddWithValue("@did", discussionId.Value.ToString());

        var result = cmd.ExecuteScalar();
        return Task.FromResult(result is long l ? l : 0L);
    }

    public Task SetCursorAsync(RoomId roomId, DiscussionId discussionId, long cursor, CancellationToken ct = default)
    {
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = """
            INSERT OR REPLACE INTO acknowledgements (room_id, discussion_id, cursor, updated_at)
            VALUES (@rid, @did, @cursor, @updated)
            """;
        cmd.Parameters.AddWithValue("@rid", roomId.Value.ToString());
        cmd.Parameters.AddWithValue("@did", discussionId.Value.ToString());
        cmd.Parameters.AddWithValue("@cursor", cursor);
        cmd.Parameters.AddWithValue("@updated", DateTimeOffset.UtcNow.ToString("O"));
        cmd.ExecuteNonQuery();
        return Task.CompletedTask;
    }
}
