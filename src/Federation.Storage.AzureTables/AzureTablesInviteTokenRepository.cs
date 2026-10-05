using Azure.Data.Tables;
using Federation.Protocol;

namespace Federation.Storage.AzureTables;

public sealed class AzureTablesInviteTokenRepository : IInviteTokenRepository
{
    private readonly TableClient _table;
    private const string PartitionKey = "token";

    public AzureTablesInviteTokenRepository(string connectionString)
    {
        _table = new TableClient(connectionString, "InviteTokens");
    }

    public async Task<InviteToken> CreateAsync(RoomId roomId, MemberRole role, int maxUses = 1, DateTimeOffset? expiresAt = null, CancellationToken ct = default)
    {
        await _table.CreateIfNotExistsAsync(ct).ConfigureAwait(false);
        var code = GenerateCode();
        var token = new InviteToken
        {
            TokenCode = code,
            RoomId    = roomId,
            Role      = role,
            MaxUses   = maxUses,
            UsedCount = 0,
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = expiresAt,
            IsRevoked = false,
        };
        var entity = ToEntity(token);
        await _table.AddEntityAsync(entity, ct).ConfigureAwait(false);
        return token;
    }

    public async Task<InviteToken?> ValidateAndConsumeAsync(string tokenCode, CancellationToken ct = default)
    {
        await _table.CreateIfNotExistsAsync(ct).ConfigureAwait(false);
        try
        {
            var resp = await _table.GetEntityAsync<TableEntity>(PartitionKey, tokenCode.ToUpperInvariant(), cancellationToken: ct).ConfigureAwait(false);
            var token = FromEntity(resp.Value);
            if (!token.IsValid) return null;
            token.UsedCount++;
            resp.Value["UsedCount"] = token.UsedCount;
            await _table.UpdateEntityAsync(resp.Value, resp.Value.ETag, TableUpdateMode.Replace, ct).ConfigureAwait(false);
            return token;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<InviteToken>> ListAsync(CancellationToken ct = default)
    {
        await _table.CreateIfNotExistsAsync(ct).ConfigureAwait(false);
        var results = new List<InviteToken>();
        await foreach (var entity in _table.QueryAsync<TableEntity>(filter: $"PartitionKey eq '{PartitionKey}'", cancellationToken: ct).ConfigureAwait(false))
            results.Add(FromEntity(entity));
        return results;
    }

    public async Task<bool> RevokeAsync(string tokenCode, CancellationToken ct = default)
    {
        await _table.CreateIfNotExistsAsync(ct).ConfigureAwait(false);
        try
        {
            var resp = await _table.GetEntityAsync<TableEntity>(PartitionKey, tokenCode.ToUpperInvariant(), cancellationToken: ct).ConfigureAwait(false);
            resp.Value["IsRevoked"] = true;
            await _table.UpdateEntityAsync(resp.Value, resp.Value.ETag, TableUpdateMode.Replace, ct).ConfigureAwait(false);
            return true;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            return false;
        }
    }

    private static TableEntity ToEntity(InviteToken t) => new(PartitionKey, t.TokenCode.ToUpperInvariant())
    {
        ["RoomId"]    = t.RoomId.Value.ToString(),
        ["Role"]      = t.Role.ToString(),
        ["MaxUses"]   = t.MaxUses,
        ["UsedCount"] = t.UsedCount,
        ["CreatedAt"] = t.CreatedAt,
        ["ExpiresAt"] = t.ExpiresAt,
        ["IsRevoked"] = t.IsRevoked,
    };

    private static InviteToken FromEntity(TableEntity e) => new()
    {
        TokenCode = e.RowKey,
        RoomId    = new RoomId(Guid.Parse(e.GetString("RoomId")!)),
        Role      = Enum.Parse<MemberRole>(e.GetString("Role")!),
        MaxUses   = e.GetInt32("MaxUses") ?? 0,
        UsedCount = e.GetInt32("UsedCount") ?? 0,
        CreatedAt = e.GetDateTimeOffset("CreatedAt") ?? DateTimeOffset.UtcNow,
        ExpiresAt = e.GetDateTimeOffset("ExpiresAt"),
        IsRevoked = e.GetBoolean("IsRevoked") ?? false,
    };

    private static string GenerateCode()
    {
        var bytes = System.Security.Cryptography.RandomNumberGenerator.GetBytes(8);
        return Convert.ToHexString(bytes).ToUpperInvariant();
    }
}
