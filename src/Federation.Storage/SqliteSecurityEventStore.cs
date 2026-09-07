using Federation.Protocol;

namespace Federation.Storage;

/// <summary>SQLite-backed security event store.</summary>
public sealed class SqliteSecurityEventStore : ISecurityEventStore
{
    private readonly FederationDatabase _db;

    public SqliteSecurityEventStore(FederationDatabase db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    public Task AppendAsync(SecurityEvent securityEvent, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(securityEvent);

        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO security_events (event_id, occurred_at, event_type, description, device_id, room_id)
            VALUES (@id, @occurred, @type, @desc, @did, @rid)
            """;
        cmd.Parameters.AddWithValue("@id", securityEvent.EventId.ToString());
        cmd.Parameters.AddWithValue("@occurred", securityEvent.OccurredAt.ToString("O"));
        cmd.Parameters.AddWithValue("@type", securityEvent.EventType);
        cmd.Parameters.AddWithValue("@desc", securityEvent.Description);
        cmd.Parameters.AddWithValue("@did", securityEvent.DeviceId.HasValue ? securityEvent.DeviceId.Value.Value.ToString() : DBNull.Value);
        cmd.Parameters.AddWithValue("@rid", securityEvent.RoomId.HasValue ? securityEvent.RoomId.Value.Value.ToString() : DBNull.Value);
        cmd.ExecuteNonQuery();

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<SecurityEvent>> GetEventsAsync(RoomId? roomId, int limit = 100, CancellationToken ct = default)
    {
        using var cmd = _db.Connection.CreateCommand();
        if (roomId.HasValue)
        {
            cmd.CommandText = "SELECT event_id, occurred_at, event_type, description, device_id, room_id FROM security_events WHERE room_id = @rid ORDER BY occurred_at DESC LIMIT @limit";
            cmd.Parameters.AddWithValue("@rid", roomId.Value.Value.ToString());
        }
        else
        {
            cmd.CommandText = "SELECT event_id, occurred_at, event_type, description, device_id, room_id FROM security_events ORDER BY occurred_at DESC LIMIT @limit";
        }
        cmd.Parameters.AddWithValue("@limit", limit);

        var list = new List<SecurityEvent>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new SecurityEvent
            {
                EventId = Guid.Parse(reader.GetString(0)),
                OccurredAt = DateTimeOffset.Parse(reader.GetString(1), System.Globalization.CultureInfo.InvariantCulture),
                EventType = reader.GetString(2),
                Description = reader.GetString(3),
                DeviceId = reader.IsDBNull(4) ? null : new DeviceId(Guid.Parse(reader.GetString(4))),
                RoomId = reader.IsDBNull(5) ? null : new RoomId(Guid.Parse(reader.GetString(5))),
            });
        }

        return Task.FromResult<IReadOnlyList<SecurityEvent>>(list);
    }
}
