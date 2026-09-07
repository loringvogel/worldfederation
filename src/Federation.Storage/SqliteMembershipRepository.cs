using Federation.Protocol;
using Microsoft.Data.Sqlite;

namespace Federation.Storage;

/// <summary>SQLite-backed membership repository.</summary>
public sealed class SqliteMembershipRepository : IMembershipRepository
{
    private readonly FederationDatabase _db;

    public SqliteMembershipRepository(FederationDatabase db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    public Task<MembershipRecord?> GetMembershipAsync(MembershipId membershipId, CancellationToken ct = default)
    {
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = "SELECT membership_id, room_id, device_id, role, joined_at, revoked_at FROM memberships WHERE membership_id = @id";
        cmd.Parameters.AddWithValue("@id", membershipId.Value.ToString());

        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return Task.FromResult<MembershipRecord?>(null);
        return Task.FromResult<MembershipRecord?>(ReadMembership(reader));
    }

    public Task<MembershipRecord?> GetMembershipByDeviceAsync(RoomId roomId, DeviceId deviceId, CancellationToken ct = default)
    {
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = "SELECT membership_id, room_id, device_id, role, joined_at, revoked_at FROM memberships WHERE room_id = @rid AND device_id = @did AND revoked_at IS NULL";
        cmd.Parameters.AddWithValue("@rid", roomId.Value.ToString());
        cmd.Parameters.AddWithValue("@did", deviceId.Value.ToString());

        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return Task.FromResult<MembershipRecord?>(null);
        return Task.FromResult<MembershipRecord?>(ReadMembership(reader));
    }

    public Task<IReadOnlyList<MembershipRecord>> GetActiveMembershipsAsync(RoomId roomId, CancellationToken ct = default)
    {
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = "SELECT membership_id, room_id, device_id, role, joined_at, revoked_at FROM memberships WHERE room_id = @rid AND revoked_at IS NULL";
        cmd.Parameters.AddWithValue("@rid", roomId.Value.ToString());

        var list = new List<MembershipRecord>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) list.Add(ReadMembership(reader));
        return Task.FromResult<IReadOnlyList<MembershipRecord>>(list);
    }

    public Task<IReadOnlyList<DeviceId>> GetDevicesForRoomAsync(RoomId roomId, CancellationToken ct = default)
    {
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = "SELECT device_id FROM memberships WHERE room_id = @rid AND revoked_at IS NULL";
        cmd.Parameters.AddWithValue("@rid", roomId.Value.ToString());

        var list = new List<DeviceId>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) list.Add(new DeviceId(Guid.Parse(reader.GetString(0))));
        return Task.FromResult<IReadOnlyList<DeviceId>>(list);
    }

    public Task<MembershipRecord> AddMembershipAsync(RoomId roomId, DeviceId deviceId, MemberRole role, CancellationToken ct = default)
    {
        var membershipId = MembershipId.New();
        var now = DateTimeOffset.UtcNow;

        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = "INSERT INTO memberships (membership_id, room_id, device_id, role, joined_at) VALUES (@mid, @rid, @did, @role, @joined)";
        cmd.Parameters.AddWithValue("@mid", membershipId.Value.ToString());
        cmd.Parameters.AddWithValue("@rid", roomId.Value.ToString());
        cmd.Parameters.AddWithValue("@did", deviceId.Value.ToString());
        cmd.Parameters.AddWithValue("@role", role.ToString());
        cmd.Parameters.AddWithValue("@joined", now.ToString("O"));
        cmd.ExecuteNonQuery();

        return Task.FromResult(new MembershipRecord
        {
            MembershipId = membershipId,
            RoomId = roomId,
            DeviceId = deviceId,
            Role = role,
            JoinedAt = now,
        });
    }

    public Task RevokeMembershipAsync(MembershipId membershipId, CancellationToken ct = default)
    {
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = "UPDATE memberships SET revoked_at = @revoked WHERE membership_id = @id";
        cmd.Parameters.AddWithValue("@revoked", DateTimeOffset.UtcNow.ToString("O"));
        cmd.Parameters.AddWithValue("@id", membershipId.Value.ToString());
        cmd.ExecuteNonQuery();
        return Task.CompletedTask;
    }

    private static MembershipRecord ReadMembership(SqliteDataReader reader)
    {
        var revokedStr = reader.IsDBNull(5) ? null : reader.GetString(5);
        return new MembershipRecord
        {
            MembershipId = new MembershipId(Guid.Parse(reader.GetString(0))),
            RoomId = new RoomId(Guid.Parse(reader.GetString(1))),
            DeviceId = new DeviceId(Guid.Parse(reader.GetString(2))),
            Role = Enum.Parse<MemberRole>(reader.GetString(3)),
            JoinedAt = DateTimeOffset.Parse(reader.GetString(4), System.Globalization.CultureInfo.InvariantCulture),
            RevokedAt = revokedStr is not null ? DateTimeOffset.Parse(revokedStr, System.Globalization.CultureInfo.InvariantCulture) : null,
        };
    }
}
