using Azure;
using Azure.Data.Tables;
using Federation.Protocol;

namespace Federation.Storage.AzureTables;

internal sealed class AzureTablesMembershipRepository : IMembershipRepository
{
    private readonly TableClient _table;

    // Room repository is injected lazily to break the circular dependency:
    // RoomRepository -> MembershipRepository -> RoomRepository (for member count updates)
    private AzureTablesRoomRepository? _roomRepo;

    internal void SetRoomRepository(AzureTablesRoomRepository roomRepo) => _roomRepo = roomRepo;

    public AzureTablesMembershipRepository(TableServiceClient serviceClient)
    {
        _table = serviceClient.GetTableClient("memberships");
        _table.CreateIfNotExists();
    }

    public async Task<MembershipRecord?> GetMembershipAsync(MembershipId membershipId, CancellationToken ct)
    {
        // We need to find the entity — we only know the membership ID, not the room ID.
        // Query across all partitions filtering on RowKey.
        var filter = $"RowKey eq '{membershipId.Value:N}'";
        var pages = _table.QueryAsync<TableEntity>(filter: filter, cancellationToken: ct);

        await foreach (var entity in pages.ConfigureAwait(false))
        {
            return ToRecord(entity);
        }

        return null;
    }

    public async Task<MembershipRecord?> GetMembershipByDeviceAsync(RoomId roomId, DeviceId deviceId, CancellationToken ct)
    {
        var pk = "bydevice_" + deviceId.Value.ToString("N");
        var rk = roomId.Value.ToString("N");

        try
        {
            var lookup = await _table.GetEntityAsync<TableEntity>(pk, rk, cancellationToken: ct)
                .ConfigureAwait(false);
            var membershipIdStr = lookup.Value.GetString("MembershipId") ?? string.Empty;
            if (!Guid.TryParse(membershipIdStr, out var membershipGuid))
                return null;

            return await GetMembershipAsync(new MembershipId(membershipGuid), ct).ConfigureAwait(false);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<MembershipRecord>> GetActiveMembershipsAsync(RoomId roomId, CancellationToken ct)
    {
        var pk = roomId.Value.ToString("N");
        var results = new List<MembershipRecord>();

        var pages = _table.QueryAsync<TableEntity>(
            filter: $"PartitionKey eq '{pk}'", cancellationToken: ct);

        await foreach (var entity in pages.ConfigureAwait(false))
        {
            // Skip secondary lookup rows (those don't have a RoomId property in the main format)
            if (!IsMainRow(entity)) continue;
            var record = ToRecord(entity);
            if (record.IsActive)
                results.Add(record);
        }

        return results;
    }

    public async Task<IReadOnlyList<DeviceId>> GetDevicesForRoomAsync(RoomId roomId, CancellationToken ct)
    {
        var memberships = await GetActiveMembershipsAsync(roomId, ct).ConfigureAwait(false);
        return memberships.Select(m => m.DeviceId).ToList();
    }

    public async Task<MembershipRecord> AddMembershipAsync(
        RoomId roomId, DeviceId deviceId, MemberRole role, CancellationToken ct)
    {
        var membershipId = new MembershipId(Guid.NewGuid());
        var now = DateTimeOffset.UtcNow;

        // Main row
        var entity = new TableEntity(roomId.Value.ToString("N"), membershipId.Value.ToString("N"))
        {
            ["MembershipId"] = membershipId.Value.ToString("N"),
            ["RoomId"] = roomId.Value.ToString("N"),
            ["DeviceId"] = deviceId.Value.ToString("N"),
            ["Role"] = role.ToString(),
            ["JoinedAt"] = now.ToString("O"),
            ["RevokedAt"] = string.Empty
        };

        await _table.AddEntityAsync(entity, ct).ConfigureAwait(false);

        // Secondary lookup row: bydevice_{deviceId} / {roomId} -> membershipId
        var lookup = new TableEntity(
            "bydevice_" + deviceId.Value.ToString("N"),
            roomId.Value.ToString("N"))
        {
            ["MembershipId"] = membershipId.Value.ToString("N")
        };

        await _table.UpsertEntityAsync(lookup, TableUpdateMode.Replace, ct).ConfigureAwait(false);

        // Update room member count
        if (_roomRepo is not null)
            await _roomRepo.UpdateMemberCountAsync(roomId, +1, ct).ConfigureAwait(false);

        return new MembershipRecord { MembershipId = membershipId, RoomId = roomId, DeviceId = deviceId, Role = role, JoinedAt = now, RevokedAt = null };
    }

    public async Task RevokeMembershipAsync(MembershipId membershipId, CancellationToken ct)
    {
        var record = await GetMembershipAsync(membershipId, ct).ConfigureAwait(false);
        if (record is null) return;

        var patch = new TableEntity(record.RoomId.Value.ToString("N"), membershipId.Value.ToString("N"))
        {
            ["RevokedAt"] = DateTimeOffset.UtcNow.ToString("O")
        };

        await _table.UpdateEntityAsync(patch, ETag.All, TableUpdateMode.Merge, ct)
            .ConfigureAwait(false);

        // Update room member count
        if (_roomRepo is not null)
            await _roomRepo.UpdateMemberCountAsync(record.RoomId, -1, ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<RoomId>> GetRoomsForDeviceAsync(DeviceId deviceId, CancellationToken ct)
    {
        // Full table scan filtering on DeviceId property, return active rooms
        var deviceIdStr = deviceId.Value.ToString("N");
        var filter = $"DeviceId eq '{deviceIdStr}'";
        var results = new List<RoomId>();

        var pages = _table.QueryAsync<TableEntity>(filter: filter, cancellationToken: ct);

        await foreach (var entity in pages.ConfigureAwait(false))
        {
            if (!IsMainRow(entity)) continue;
            var revokedAt = entity.GetString("RevokedAt") ?? string.Empty;
            if (string.IsNullOrEmpty(revokedAt)) // IsActive
            {
                var roomIdStr = entity.GetString("RoomId") ?? string.Empty;
                if (Guid.TryParse(roomIdStr, out var roomGuid))
                    results.Add(new RoomId(roomGuid));
            }
        }

        return results;
    }

    private static bool IsMainRow(TableEntity entity)
    {
        // Main rows have a RoomId property; secondary lookup rows (bydevice_*) have MembershipId but no Role
        return entity.ContainsKey("Role");
    }

    private static MembershipRecord ToRecord(TableEntity e)
    {
        var membershipIdStr = e.GetString("MembershipId") ?? e.RowKey;
        var membershipId = new MembershipId(Guid.Parse(membershipIdStr));
        var roomId = new RoomId(Guid.Parse(e.GetString("RoomId") ?? e.PartitionKey));
        var deviceId = new DeviceId(Guid.Parse(e.GetString("DeviceId") ?? string.Empty));
        var role = Enum.Parse<MemberRole>(e.GetString("Role") ?? "Participant");
        var joinedAt = DateTimeOffset.Parse(e.GetString("JoinedAt") ?? DateTimeOffset.MinValue.ToString("O"));
        var revokedAtStr = e.GetString("RevokedAt") ?? string.Empty;
        DateTimeOffset? revokedAt = string.IsNullOrEmpty(revokedAtStr)
            ? null
            : DateTimeOffset.Parse(revokedAtStr);

        return new MembershipRecord { MembershipId = membershipId, RoomId = roomId, DeviceId = deviceId, Role = role, JoinedAt = joinedAt, RevokedAt = revokedAt };
    }
}
