using System.Text.Json;
using Federation.Sync;

namespace Federation.Storage;

/// <summary>SQLite-backed event log for federation sync.</summary>
public sealed class SqliteEventLog : IEventLog
{
    private readonly FederationDatabase _db;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public SqliteEventLog(FederationDatabase db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    public Task AppendAsync(FederationEvent federationEvent, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(federationEvent);

        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = """
            INSERT OR IGNORE INTO federation_events (event_id, event_type, occurred_at, parent_event_id, payload, signature)
            VALUES (@id, @type, @occurred, @parent, @payload, @sig)
            """;
        cmd.Parameters.AddWithValue("@id", federationEvent.EventId.ToString());
        cmd.Parameters.AddWithValue("@type", federationEvent.EventType);
        cmd.Parameters.AddWithValue("@occurred", federationEvent.OccurredAt.ToString("O"));
        cmd.Parameters.AddWithValue("@parent", federationEvent.ParentEventId ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@payload", JsonSerializer.Serialize<object>(federationEvent, JsonOptions));
        cmd.Parameters.AddWithValue("@sig", federationEvent.Signature);
        cmd.ExecuteNonQuery();

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<FederationEvent>> GetEventsAsync(string? afterEventId, CancellationToken ct = default)
    {
        // For simplicity, return all events ordered by rowid (insertion order)
        // In a real implementation, we would track sequence numbers
        using var cmd = _db.Connection.CreateCommand();
        if (afterEventId is not null)
        {
            cmd.CommandText = """
                SELECT event_id, event_type, occurred_at, parent_event_id, payload, signature FROM federation_events
                WHERE rowid > (SELECT rowid FROM federation_events WHERE event_id = @after)
                ORDER BY rowid
                """;
            cmd.Parameters.AddWithValue("@after", afterEventId);
        }
        else
        {
            cmd.CommandText = "SELECT event_id, event_type, occurred_at, parent_event_id, payload, signature FROM federation_events ORDER BY rowid";
        }

        var list = new List<FederationEvent>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(ReadEvent(reader));
        }

        return Task.FromResult<IReadOnlyList<FederationEvent>>(list);
    }

    public Task<FederationEvent?> GetEventAsync(string eventId, CancellationToken ct = default)
    {
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = "SELECT event_id, event_type, occurred_at, parent_event_id, payload, signature FROM federation_events WHERE event_id = @id";
        cmd.Parameters.AddWithValue("@id", eventId);

        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return Task.FromResult<FederationEvent?>(null);
        return Task.FromResult<FederationEvent?>(ReadEvent(reader));
    }

    private static FederationEvent ReadEvent(Microsoft.Data.Sqlite.SqliteDataReader reader)
    {
        // Return as a generic MessageAcceptedEvent since we store serialized JSON
        // In a real implementation, we would deserialize based on event_type
        return new MessageAcceptedEvent
        {
            EventId = Guid.Parse(reader.GetString(0)),
            EventType = reader.GetString(1),
            OccurredAt = DateTimeOffset.Parse(reader.GetString(2), System.Globalization.CultureInfo.InvariantCulture),
            ParentEventId = reader.IsDBNull(3) ? null : reader.GetString(3),
            Signature = (byte[])reader.GetValue(5),
            Payload = new MessageAcceptedPayload
            {
                RoomId = new Federation.Protocol.RoomId(Guid.Empty),
                DiscussionId = new Federation.Protocol.DiscussionId(Guid.Empty),
                MessageId = new Federation.Protocol.MessageId(Guid.Empty),
            },
        };
    }
}
