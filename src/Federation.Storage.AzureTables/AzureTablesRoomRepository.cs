using Azure;
using Azure.Data.Tables;
using Federation.Protocol;

namespace Federation.Storage.AzureTables;

internal sealed class AzureTablesRoomRepository : IRoomRepository
{
    private readonly TableClient _table;
    private readonly IMembershipRepository _memberships;

    public AzureTablesRoomRepository(TableServiceClient serviceClient, IMembershipRepository memberships)
    {
        _table = serviceClient.GetTableClient("rooms");
        _table.CreateIfNotExists();
        _memberships = memberships;
    }

    public async Task<RoomSummary> CreateRoomAsync(CreateRoomRequest request, CancellationToken ct)
    {
        var roomId = new RoomId(Guid.NewGuid());
        var now = DateTimeOffset.UtcNow;

        var entity = new TableEntity("all", roomId.Value.ToString("N"))
        {
            ["Name"] = request.Name,
            ["CreatedAt"] = now.ToString("O"),
            ["MemberCount"] = 0,
            ["ActiveDiscussionCount"] = 0
        };

        await _table.AddEntityAsync(entity, ct).ConfigureAwait(false);

        // Add owner membership — this also updates MemberCount via AddMembershipAsync
        await _memberships.AddMembershipAsync(roomId, request.OwnerDeviceId, MemberRole.Owner, ct)
            .ConfigureAwait(false);

        var summary = await GetRoomAsync(roomId, ct).ConfigureAwait(false);
        return summary!;
    }

    public async Task<RoomSummary?> GetRoomAsync(RoomId roomId, CancellationToken ct)
    {
        try
        {
            var response = await _table.GetEntityAsync<TableEntity>(
                "all", roomId.Value.ToString("N"), cancellationToken: ct).ConfigureAwait(false);
            return ToSummary(roomId, response.Value);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<RoomSummary>> ListRoomsAsync(CancellationToken ct)
    {
        var results = new List<RoomSummary>();
        var pages = _table.QueryAsync<TableEntity>(
            filter: $"PartitionKey eq 'all'", cancellationToken: ct);

        await foreach (var entity in pages.ConfigureAwait(false))
        {
            if (Guid.TryParse(entity.RowKey, out var guid))
            {
                results.Add(ToSummary(new RoomId(guid), entity));
            }
        }

        return results;
    }

    internal async Task UpdateMemberCountAsync(RoomId roomId, int delta, CancellationToken ct)
    {
        try
        {
            var response = await _table.GetEntityAsync<TableEntity>(
                "all", roomId.Value.ToString("N"), cancellationToken: ct).ConfigureAwait(false);
            var entity = response.Value;
            var current = entity.GetInt32("MemberCount") ?? 0;

            var patch = new TableEntity("all", roomId.Value.ToString("N"))
            {
                ["MemberCount"] = Math.Max(0, current + delta)
            };
            patch.ETag = ETag.All;

            await _table.UpdateEntityAsync(patch, ETag.All, TableUpdateMode.Merge, ct)
                .ConfigureAwait(false);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            // room doesn't exist, ignore
        }
    }

    private static RoomSummary ToSummary(RoomId roomId, TableEntity e)
    {
        var name = e.GetString("Name") ?? string.Empty;
        var createdAt = DateTimeOffset.Parse(e.GetString("CreatedAt") ?? DateTimeOffset.MinValue.ToString("O"));
        var memberCount = e.GetInt32("MemberCount") ?? 0;
        var activeDiscussionCount = e.GetInt32("ActiveDiscussionCount") ?? 0;
        return new RoomSummary { RoomId = roomId, Name = name, CreatedAt = createdAt, MemberCount = memberCount, ActiveDiscussionCount = activeDiscussionCount };
    }
}
