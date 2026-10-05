using Azure.Data.Tables;
using Federation.Protocol;

namespace Federation.Storage.AzureTables;

internal sealed class AzureTablesSecurityEventStore : ISecurityEventStore
{
    private readonly TableClient _table;

    public AzureTablesSecurityEventStore(TableServiceClient serviceClient)
    {
        _table = serviceClient.GetTableClient("secevents");
        _table.CreateIfNotExists();
    }

    public async Task AppendAsync(SecurityEvent securityEvent, CancellationToken ct)
    {
        var pk = securityEvent.RoomId.HasValue
            ? securityEvent.RoomId.Value.Value.ToString("N")
            : "global";

        var entity = new TableEntity(pk, securityEvent.EventId.ToString("N"))
        {
            ["EventId"] = securityEvent.EventId.ToString("N"),
            ["OccurredAt"] = securityEvent.OccurredAt.ToString("O"),
            ["EventType"] = securityEvent.EventType,
            ["Description"] = securityEvent.Description,
            ["DeviceId"] = securityEvent.DeviceId.HasValue
                ? securityEvent.DeviceId.Value.Value.ToString("N")
                : string.Empty,
            ["RoomId"] = securityEvent.RoomId.HasValue
                ? securityEvent.RoomId.Value.Value.ToString("N")
                : string.Empty
        };

        await _table.AddEntityAsync(entity, ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<SecurityEvent>> GetEventsAsync(
        RoomId? roomId, int limit, CancellationToken ct)
    {
        string filter;
        if (roomId.HasValue)
        {
            var pk = roomId.Value.Value.ToString("N");
            filter = $"PartitionKey eq '{pk}'";
        }
        else
        {
            filter = string.Empty; // full scan for all events
        }

        var results = new List<SecurityEvent>();

        var pages = string.IsNullOrEmpty(filter)
            ? _table.QueryAsync<TableEntity>(cancellationToken: ct)
            : _table.QueryAsync<TableEntity>(filter: filter, cancellationToken: ct);

        await foreach (var entity in pages.ConfigureAwait(false))
        {
            results.Add(ToEvent(entity));
            if (results.Count >= limit) break;
        }

        return results;
    }

    private static SecurityEvent ToEvent(TableEntity e)
    {
        var eventId = Guid.Parse(e.GetString("EventId") ?? e.RowKey);
        var occurredAt = DateTimeOffset.Parse(e.GetString("OccurredAt") ?? DateTimeOffset.MinValue.ToString("O"));
        var eventType = e.GetString("EventType") ?? string.Empty;
        var description = e.GetString("Description") ?? string.Empty;

        var deviceIdStr = e.GetString("DeviceId") ?? string.Empty;
        DeviceId? deviceId = string.IsNullOrEmpty(deviceIdStr)
            ? null
            : new DeviceId(Guid.Parse(deviceIdStr));

        var roomIdStr = e.GetString("RoomId") ?? string.Empty;
        RoomId? roomId = string.IsNullOrEmpty(roomIdStr)
            ? null
            : new RoomId(Guid.Parse(roomIdStr));

        return new SecurityEvent { EventId = eventId, OccurredAt = occurredAt, EventType = eventType, Description = description, DeviceId = deviceId, RoomId = roomId };
    }
}
