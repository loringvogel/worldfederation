using Federation.Identity;
using Federation.Protocol;

namespace Federation.Storage;

/// <summary>SQLite-backed identity store.</summary>
public sealed class SqliteIdentityStore : IIdentityStore
{
    private readonly FederationDatabase _db;

    public SqliteIdentityStore(FederationDatabase db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    public Task StoreDeviceIdentityAsync(DeviceIdentity identity, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(identity);

        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = """
            INSERT OR REPLACE INTO device_identities (device_id, display_name, public_signing_key, public_key_agreement_key, created_at)
            VALUES (@id, @name, @sigkey, @agreekey, @created)
            """;
        cmd.Parameters.AddWithValue("@id", identity.Id.Value.ToString());
        cmd.Parameters.AddWithValue("@name", identity.DisplayName);
        cmd.Parameters.AddWithValue("@sigkey", identity.PublicSigningKey);
        cmd.Parameters.AddWithValue("@agreekey", identity.PublicKeyAgreementKey);
        cmd.Parameters.AddWithValue("@created", identity.CreatedAt.ToString("O"));
        cmd.ExecuteNonQuery();

        return Task.CompletedTask;
    }

    public Task<DeviceIdentity?> LoadDeviceIdentityAsync(DeviceId deviceId, CancellationToken ct = default)
    {
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = "SELECT device_id, display_name, public_signing_key, public_key_agreement_key, created_at FROM device_identities WHERE device_id = @id";
        cmd.Parameters.AddWithValue("@id", deviceId.Value.ToString());

        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return Task.FromResult<DeviceIdentity?>(null);

        return Task.FromResult<DeviceIdentity?>(new DeviceIdentity
        {
            Id = new DeviceId(Guid.Parse(reader.GetString(0))),
            DisplayName = reader.GetString(1),
            PublicSigningKey = (byte[])reader.GetValue(2),
            PublicKeyAgreementKey = (byte[])reader.GetValue(3),
            CreatedAt = DateTimeOffset.Parse(reader.GetString(4), System.Globalization.CultureInfo.InvariantCulture),
        });
    }

    public Task StorePeerRecordAsync(PeerRecord peerRecord, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(peerRecord);

        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = """
            INSERT OR REPLACE INTO peer_records (device_id, human_id, addresses, supported_protocol_versions, issued_at, expires_at, capabilities, signature)
            VALUES (@did, @hid, @addrs, @protos, @issued, @expires, @caps, @sig)
            """;
        cmd.Parameters.AddWithValue("@did", peerRecord.DeviceId.Value.ToString());
        cmd.Parameters.AddWithValue("@hid", peerRecord.HumanId.Value.ToString());
        cmd.Parameters.AddWithValue("@addrs", string.Join("|", peerRecord.Addresses));
        cmd.Parameters.AddWithValue("@protos", string.Join("|", peerRecord.SupportedProtocolVersions));
        cmd.Parameters.AddWithValue("@issued", peerRecord.IssuedAt.ToString("O"));
        cmd.Parameters.AddWithValue("@expires", peerRecord.ExpiresAt.ToString("O"));
        cmd.Parameters.AddWithValue("@caps", string.Join("|", peerRecord.Capabilities));
        cmd.Parameters.AddWithValue("@sig", peerRecord.Signature);
        cmd.ExecuteNonQuery();

        return Task.CompletedTask;
    }

    public Task<PeerRecord?> GetPeerRecordAsync(DeviceId deviceId, CancellationToken ct = default)
    {
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = "SELECT device_id, human_id, addresses, supported_protocol_versions, issued_at, expires_at, capabilities, signature FROM peer_records WHERE device_id = @id";
        cmd.Parameters.AddWithValue("@id", deviceId.Value.ToString());

        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return Task.FromResult<PeerRecord?>(null);

        return Task.FromResult<PeerRecord?>(ReadPeerRecord(reader));
    }

    public Task<IReadOnlyList<PeerRecord>> GetPeersForRoomAsync(RoomId roomId, CancellationToken ct = default)
    {
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = """
            SELECT p.device_id, p.human_id, p.addresses, p.supported_protocol_versions, p.issued_at, p.expires_at, p.capabilities, p.signature
            FROM peer_records p
            INNER JOIN room_peer_memberships m ON p.device_id = m.device_id
            WHERE m.room_id = @rid
            """;
        cmd.Parameters.AddWithValue("@rid", roomId.Value.ToString());

        var list = new List<PeerRecord>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) list.Add(ReadPeerRecord(reader));
        return Task.FromResult<IReadOnlyList<PeerRecord>>(list);
    }

    public Task StorePeerRecordForRoomAsync(RoomId roomId, PeerRecord record, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        // Upsert the peer record
        StorePeerRecordAsync(record, ct);

        // Insert room membership
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = """
            INSERT OR IGNORE INTO room_peer_memberships (room_id, device_id, added_at)
            VALUES (@rid, @did, @added)
            """;
        cmd.Parameters.AddWithValue("@rid", roomId.Value.ToString());
        cmd.Parameters.AddWithValue("@did", record.DeviceId.Value.ToString());
        cmd.Parameters.AddWithValue("@added", DateTimeOffset.UtcNow.ToString("O"));
        cmd.ExecuteNonQuery();

        return Task.CompletedTask;
    }

    private static PeerRecord ReadPeerRecord(Microsoft.Data.Sqlite.SqliteDataReader reader)
    {
        return new PeerRecord
        {
            DeviceId = new DeviceId(Guid.Parse(reader.GetString(0))),
            HumanId = new HumanId(Guid.Parse(reader.GetString(1))),
            Addresses = reader.GetString(2).Split('|', StringSplitOptions.RemoveEmptyEntries),
            SupportedProtocolVersions = reader.GetString(3).Split('|', StringSplitOptions.RemoveEmptyEntries),
            IssuedAt = DateTimeOffset.Parse(reader.GetString(4), System.Globalization.CultureInfo.InvariantCulture),
            ExpiresAt = DateTimeOffset.Parse(reader.GetString(5), System.Globalization.CultureInfo.InvariantCulture),
            Capabilities = reader.GetString(6).Split('|', StringSplitOptions.RemoveEmptyEntries),
            Signature = (byte[])reader.GetValue(7),
        };
    }
}
