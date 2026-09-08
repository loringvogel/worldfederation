using Federation.Protocol;
using Microsoft.Data.Sqlite;

namespace Federation.Storage;

/// <summary>SQLite-backed discussion repository.</summary>
public sealed class SqliteDiscussionRepository : IDiscussionRepository
{
    private readonly FederationDatabase _db;

    public SqliteDiscussionRepository(FederationDatabase db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    public Task<DiscussionState> CreateDiscussionAsync(CreateDiscussionRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var id = DiscussionId.New();
        var now = DateTimeOffset.UtcNow;

        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO discussions (discussion_id, room_id, topic, phase, current_round, created_at, total_submissions, expected_submissions)
            VALUES (@id, @rid, @topic, @phase, 0, @created, 0, 0)
            """;
        cmd.Parameters.AddWithValue("@id", id.Value.ToString());
        cmd.Parameters.AddWithValue("@rid", request.RoomId.Value.ToString());
        cmd.Parameters.AddWithValue("@topic", request.Topic);
        cmd.Parameters.AddWithValue("@phase", DiscussionPhase.Draft.ToString());
        cmd.Parameters.AddWithValue("@created", now.ToString("O"));
        cmd.ExecuteNonQuery();

        return Task.FromResult(new DiscussionState
        {
            DiscussionId = id,
            RoomId = request.RoomId,
            Topic = request.Topic,
            Phase = DiscussionPhase.Draft,
            CurrentRound = 0,
            CreatedAt = now,
            TotalSubmissions = 0,
            ExpectedSubmissions = 0,
        });
    }

    public Task<DiscussionState?> GetDiscussionAsync(DiscussionId discussionId, CancellationToken ct = default)
    {
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = "SELECT discussion_id, room_id, topic, phase, current_round, created_at, current_deadline, total_submissions, expected_submissions FROM discussions WHERE discussion_id = @id";
        cmd.Parameters.AddWithValue("@id", discussionId.Value.ToString());

        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return Task.FromResult<DiscussionState?>(null);

        return Task.FromResult<DiscussionState?>(ReadDiscussion(reader));
    }

    public Task UpdateDiscussionStateAsync(DiscussionId discussionId, DiscussionPhase newPhase, int newRound, DateTimeOffset? newDeadline, CancellationToken ct = default)
    {
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = "UPDATE discussions SET phase = @phase, current_round = @round, current_deadline = @deadline, total_submissions = 0 WHERE discussion_id = @id";
        cmd.Parameters.AddWithValue("@phase", newPhase.ToString());
        cmd.Parameters.AddWithValue("@round", newRound);
        cmd.Parameters.AddWithValue("@deadline", newDeadline.HasValue ? newDeadline.Value.ToString("O") : DBNull.Value);
        cmd.Parameters.AddWithValue("@id", discussionId.Value.ToString());
        cmd.ExecuteNonQuery();
        return Task.CompletedTask;
    }

    public Task IncrementSubmissionCountAsync(DiscussionId discussionId, CancellationToken ct = default)
    {
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = "UPDATE discussions SET total_submissions = total_submissions + 1 WHERE discussion_id = @id";
        cmd.Parameters.AddWithValue("@id", discussionId.Value.ToString());
        cmd.ExecuteNonQuery();
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<DiscussionState>> GetActiveDiscussionsAsync(CancellationToken ct = default)
    {
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = "SELECT discussion_id, room_id, topic, phase, current_round, created_at, current_deadline, total_submissions, expected_submissions FROM discussions WHERE phase != 'Closed'";

        var list = new List<DiscussionState>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(ReadDiscussion(reader));
        }

        return Task.FromResult<IReadOnlyList<DiscussionState>>(list);
    }

    public Task<IReadOnlyList<DiscussionState>> GetDiscussionsInRoomAsync(RoomId roomId, CancellationToken ct = default)
    {
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = "SELECT discussion_id, room_id, topic, phase, current_round, created_at, current_deadline, total_submissions, expected_submissions FROM discussions WHERE room_id = @rid";
        cmd.Parameters.AddWithValue("@rid", roomId.Value.ToString());

        var list = new List<DiscussionState>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(ReadDiscussion(reader));
        }

        return Task.FromResult<IReadOnlyList<DiscussionState>>(list);
    }

    public Task RecordSubmissionAsync(RoundId roundId, DeviceId deviceId, CancellationToken ct = default)
    {
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = "INSERT OR IGNORE INTO round_submissions (round_id, device_id, submitted_at) VALUES (@rid, @did, @sub)";
        cmd.Parameters.AddWithValue("@rid", roundId.Value.ToString());
        cmd.Parameters.AddWithValue("@did", deviceId.Value.ToString());
        cmd.Parameters.AddWithValue("@sub", DateTimeOffset.UtcNow.ToString("O"));
        cmd.ExecuteNonQuery();
        return Task.CompletedTask;
    }

    public Task<bool> HasSubmittedAsync(RoundId roundId, DeviceId deviceId, CancellationToken ct = default)
    {
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(1) FROM round_submissions WHERE round_id = @rid AND device_id = @did";
        cmd.Parameters.AddWithValue("@rid", roundId.Value.ToString());
        cmd.Parameters.AddWithValue("@did", deviceId.Value.ToString());
        var count = (long)(cmd.ExecuteScalar() ?? 0);
        return Task.FromResult(count > 0);
    }

    public Task<IReadOnlyList<DeviceId>> GetSubmittedDevicesAsync(RoundId roundId, CancellationToken ct = default)
    {
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = "SELECT device_id FROM round_submissions WHERE round_id = @rid";
        cmd.Parameters.AddWithValue("@rid", roundId.Value.ToString());

        var list = new List<DeviceId>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new DeviceId(Guid.Parse(reader.GetString(0))));
        }
        return Task.FromResult<IReadOnlyList<DeviceId>>(list);
    }

    private static DiscussionState ReadDiscussion(SqliteDataReader reader)
    {
        var deadlineStr = reader.IsDBNull(6) ? null : reader.GetString(6);
        return new DiscussionState
        {
            DiscussionId = new DiscussionId(Guid.Parse(reader.GetString(0))),
            RoomId = new RoomId(Guid.Parse(reader.GetString(1))),
            Topic = reader.GetString(2),
            Phase = Enum.Parse<DiscussionPhase>(reader.GetString(3)),
            CurrentRound = reader.GetInt32(4),
            CreatedAt = DateTimeOffset.Parse(reader.GetString(5), System.Globalization.CultureInfo.InvariantCulture),
            CurrentDeadline = deadlineStr is not null ? DateTimeOffset.Parse(deadlineStr, System.Globalization.CultureInfo.InvariantCulture) : null,
            TotalSubmissions = reader.GetInt32(7),
            ExpectedSubmissions = reader.GetInt32(8),
        };
    }
}
