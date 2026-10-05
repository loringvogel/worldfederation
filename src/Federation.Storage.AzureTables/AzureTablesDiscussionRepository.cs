using Azure;
using Azure.Data.Tables;
using Federation.Protocol;

namespace Federation.Storage.AzureTables;

internal sealed class AzureTablesDiscussionRepository : IDiscussionRepository
{
    private readonly TableClient _discussionTable;
    private readonly TableClient _roundSubsTable;

    public AzureTablesDiscussionRepository(TableServiceClient serviceClient)
    {
        _discussionTable = serviceClient.GetTableClient("discussions");
        _discussionTable.CreateIfNotExists();

        _roundSubsTable = serviceClient.GetTableClient("roundsubs");
        _roundSubsTable.CreateIfNotExists();
    }

    public async Task<DiscussionState> CreateDiscussionAsync(CreateDiscussionRequest request, CancellationToken ct)
    {
        var discussionId = new DiscussionId(Guid.NewGuid());
        var now = DateTimeOffset.UtcNow;

        var entity = new TableEntity(request.RoomId.Value.ToString("N"), discussionId.Value.ToString("N"))
        {
            ["DiscussionId"] = discussionId.Value.ToString("N"),
            ["RoomId"] = request.RoomId.Value.ToString("N"),
            ["Topic"] = request.Topic,
            ["Phase"] = DiscussionPhase.Draft.ToString(),
            ["CurrentRound"] = 0,
            ["CreatedAt"] = now.ToString("O"),
            ["CurrentDeadline"] = string.Empty,
            ["TotalSubmissions"] = 0,
            ["ExpectedSubmissions"] = 0
        };

        await _discussionTable.AddEntityAsync(entity, ct).ConfigureAwait(false);

        return new DiscussionState { DiscussionId = discussionId, RoomId = request.RoomId, Topic = request.Topic, Phase = DiscussionPhase.Draft, CurrentRound = 0, CreatedAt = now, CurrentDeadline = null, TotalSubmissions = 0, ExpectedSubmissions = 0 };
    }

    public async Task<DiscussionState?> GetDiscussionAsync(DiscussionId discussionId, CancellationToken ct)
    {
        // We need to find by discussion ID across all partitions
        var filter = $"RowKey eq '{discussionId.Value:N}'";
        var pages = _discussionTable.QueryAsync<TableEntity>(filter: filter, cancellationToken: ct);

        await foreach (var entity in pages.ConfigureAwait(false))
        {
            return ToState(entity);
        }

        return null;
    }

    public async Task UpdateDiscussionStateAsync(
        DiscussionId discussionId,
        DiscussionPhase newPhase,
        int newRound,
        DateTimeOffset? newDeadline,
        CancellationToken ct)
    {
        var existing = await GetDiscussionAsync(discussionId, ct).ConfigureAwait(false);
        if (existing is null) return;

        var patch = new TableEntity(existing.RoomId.Value.ToString("N"), discussionId.Value.ToString("N"))
        {
            ["Phase"] = newPhase.ToString(),
            ["CurrentRound"] = newRound,
            ["CurrentDeadline"] = newDeadline.HasValue ? newDeadline.Value.ToString("O") : string.Empty
        };

        await _discussionTable.UpdateEntityAsync(patch, ETag.All, TableUpdateMode.Merge, ct)
            .ConfigureAwait(false);
    }

    public async Task IncrementSubmissionCountAsync(DiscussionId discussionId, CancellationToken ct)
    {
        var existing = await GetDiscussionAsync(discussionId, ct).ConfigureAwait(false);
        if (existing is null) return;

        var patch = new TableEntity(existing.RoomId.Value.ToString("N"), discussionId.Value.ToString("N"))
        {
            ["TotalSubmissions"] = existing.TotalSubmissions + 1
        };

        await _discussionTable.UpdateEntityAsync(patch, ETag.All, TableUpdateMode.Merge, ct)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<DiscussionState>> GetActiveDiscussionsAsync(CancellationToken ct)
    {
        var results = new List<DiscussionState>();
        var pages = _discussionTable.QueryAsync<TableEntity>(cancellationToken: ct);

        await foreach (var entity in pages.ConfigureAwait(false))
        {
            var state = ToState(entity);
            if (state.Phase != DiscussionPhase.Closed)
                results.Add(state);
        }

        return results;
    }

    public async Task<IReadOnlyList<DiscussionState>> GetDiscussionsInRoomAsync(RoomId roomId, CancellationToken ct)
    {
        var pk = roomId.Value.ToString("N");
        var results = new List<DiscussionState>();

        var pages = _discussionTable.QueryAsync<TableEntity>(
            filter: $"PartitionKey eq '{pk}'", cancellationToken: ct);

        await foreach (var entity in pages.ConfigureAwait(false))
        {
            results.Add(ToState(entity));
        }

        return results;
    }

    public async Task RecordSubmissionAsync(RoundId roundId, DeviceId deviceId, CancellationToken ct)
    {
        var entity = new TableEntity(roundId.Value.ToString("N"), deviceId.Value.ToString("N"))
        {
            ["RecordedAt"] = DateTimeOffset.UtcNow.ToString("O")
        };

        await _roundSubsTable.UpsertEntityAsync(entity, TableUpdateMode.Replace, ct).ConfigureAwait(false);
    }

    public async Task<bool> HasSubmittedAsync(RoundId roundId, DeviceId deviceId, CancellationToken ct)
    {
        try
        {
            await _roundSubsTable.GetEntityAsync<TableEntity>(
                roundId.Value.ToString("N"), deviceId.Value.ToString("N"),
                cancellationToken: ct).ConfigureAwait(false);
            return true;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return false;
        }
    }

    public async Task<IReadOnlyList<DeviceId>> GetSubmittedDevicesAsync(RoundId roundId, CancellationToken ct)
    {
        var pk = roundId.Value.ToString("N");
        var results = new List<DeviceId>();

        var pages = _roundSubsTable.QueryAsync<TableEntity>(
            filter: $"PartitionKey eq '{pk}'", cancellationToken: ct);

        await foreach (var entity in pages.ConfigureAwait(false))
        {
            if (Guid.TryParse(entity.RowKey, out var guid))
                results.Add(new DeviceId(guid));
        }

        return results;
    }

    private static DiscussionState ToState(TableEntity e)
    {
        var discussionId = new DiscussionId(Guid.Parse(e.GetString("DiscussionId") ?? e.RowKey));
        var roomId = new RoomId(Guid.Parse(e.GetString("RoomId") ?? e.PartitionKey));
        var topic = e.GetString("Topic") ?? string.Empty;
        var phase = Enum.Parse<DiscussionPhase>(e.GetString("Phase") ?? "Draft");
        var currentRound = e.GetInt32("CurrentRound") ?? 0;
        var createdAt = DateTimeOffset.Parse(e.GetString("CreatedAt") ?? DateTimeOffset.MinValue.ToString("O"));
        var deadlineStr = e.GetString("CurrentDeadline") ?? string.Empty;
        DateTimeOffset? deadline = string.IsNullOrEmpty(deadlineStr)
            ? null
            : DateTimeOffset.Parse(deadlineStr);
        var totalSubmissions = e.GetInt32("TotalSubmissions") ?? 0;
        var expectedSubmissions = e.GetInt32("ExpectedSubmissions") ?? 0;

        return new DiscussionState { DiscussionId = discussionId, RoomId = roomId, Topic = topic, Phase = phase, CurrentRound = currentRound, CreatedAt = createdAt, CurrentDeadline = deadline, TotalSubmissions = totalSubmissions, ExpectedSubmissions = expectedSubmissions };
    }
}
