using Federation.Protocol;
using Microsoft.Data.Sqlite;

namespace Federation.Storage;

/// <summary>SQLite-backed room repository.</summary>
public sealed class SqliteRoomRepository : IRoomRepository
{
    private readonly FederationDatabase _db;

    public SqliteRoomRepository(FederationDatabase db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    public Task<RoomSummary> CreateRoomAsync(CreateRoomRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var roomId = RoomId.New();
        var now = DateTimeOffset.UtcNow;

        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = "INSERT INTO rooms (room_id, name, created_at, member_count, active_discussion_count, is_public) VALUES (@id, @name, @created, 1, 0, @ispublic)";
        cmd.Parameters.AddWithValue("@id", roomId.Value.ToString());
        cmd.Parameters.AddWithValue("@name", request.Name);
        cmd.Parameters.AddWithValue("@created", now.ToString("O"));
        cmd.Parameters.AddWithValue("@ispublic", request.IsPublic ? 1 : 0);
        cmd.ExecuteNonQuery();

        var summary = new RoomSummary
        {
            RoomId = roomId,
            Name = request.Name,
            CreatedAt = now,
            MemberCount = 1,
            ActiveDiscussionCount = 0,
            IsPublic = request.IsPublic,
        };

        return Task.FromResult(summary);
    }

    public Task<RoomSummary?> GetRoomAsync(RoomId roomId, CancellationToken ct = default)
    {
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = "SELECT room_id, name, created_at, member_count, active_discussion_count, is_public FROM rooms WHERE room_id = @id";
        cmd.Parameters.AddWithValue("@id", roomId.Value.ToString());

        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return Task.FromResult<RoomSummary?>(null);

        return Task.FromResult<RoomSummary?>(new RoomSummary
        {
            RoomId = new RoomId(Guid.Parse(reader.GetString(0))),
            Name = reader.GetString(1),
            CreatedAt = DateTimeOffset.Parse(reader.GetString(2), System.Globalization.CultureInfo.InvariantCulture),
            MemberCount = reader.GetInt32(3),
            ActiveDiscussionCount = reader.GetInt32(4),
            IsPublic = reader.GetInt32(5) != 0,
        });
    }

    public Task<IReadOnlyList<RoomSummary>> ListRoomsAsync(CancellationToken ct = default)
    {
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = "SELECT room_id, name, created_at, member_count, active_discussion_count, is_public FROM rooms";

        var rooms = new List<RoomSummary>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            rooms.Add(new RoomSummary
            {
                RoomId = new RoomId(Guid.Parse(reader.GetString(0))),
                Name = reader.GetString(1),
                CreatedAt = DateTimeOffset.Parse(reader.GetString(2), System.Globalization.CultureInfo.InvariantCulture),
                MemberCount = reader.GetInt32(3),
                ActiveDiscussionCount = reader.GetInt32(4),
                IsPublic = reader.GetInt32(5) != 0,
            });
        }

        return Task.FromResult<IReadOnlyList<RoomSummary>>(rooms);
    }

    public Task<IReadOnlyList<RoomSummary>> ListPublicRoomsAsync(CancellationToken ct = default)
    {
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = "SELECT room_id, name, created_at, member_count, active_discussion_count, is_public FROM rooms WHERE is_public = 1";

        var rooms = new List<RoomSummary>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            rooms.Add(new RoomSummary
            {
                RoomId = new RoomId(Guid.Parse(reader.GetString(0))),
                Name = reader.GetString(1),
                CreatedAt = DateTimeOffset.Parse(reader.GetString(2), System.Globalization.CultureInfo.InvariantCulture),
                MemberCount = reader.GetInt32(3),
                ActiveDiscussionCount = reader.GetInt32(4),
                IsPublic = reader.GetInt32(5) != 0,
            });
        }

        return Task.FromResult<IReadOnlyList<RoomSummary>>(rooms);
    }
}
