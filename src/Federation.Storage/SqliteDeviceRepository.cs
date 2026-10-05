using System.Security.Cryptography;
using Federation.Protocol;

namespace Federation.Storage;

/// <summary>SQLite-backed device repository.</summary>
public sealed class SqliteDeviceRepository : IDeviceRepository
{
    private readonly FederationDatabase _db;

    public SqliteDeviceRepository(FederationDatabase db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    public Task<DeviceRegistration> RegisterDeviceAsync(string displayName, byte[] publicKey, CancellationToken ct = default)
    {
        var deviceId = DeviceId.New();
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var now = DateTimeOffset.UtcNow;

        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO devices (device_id, display_name, public_key, registered_at, device_token)
            VALUES (@id, @name, @key, @registered, @token)
            """;
        cmd.Parameters.AddWithValue("@id", deviceId.Value.ToString());
        cmd.Parameters.AddWithValue("@name", displayName);
        cmd.Parameters.AddWithValue("@key", publicKey);
        cmd.Parameters.AddWithValue("@registered", now.ToString("O"));
        cmd.Parameters.AddWithValue("@token", token);
        cmd.ExecuteNonQuery();

        return Task.FromResult(new DeviceRegistration
        {
            DeviceId = deviceId,
            DisplayName = displayName,
            PublicKey = publicKey,
            RegisteredAt = now,
            DeviceToken = token,
        });
    }

    public Task<DeviceRegistration?> GetDeviceAsync(DeviceId deviceId, CancellationToken ct = default)
    {
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = "SELECT device_id, display_name, public_key, registered_at, device_token FROM devices WHERE device_id = @id";
        cmd.Parameters.AddWithValue("@id", deviceId.Value.ToString());

        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return Task.FromResult<DeviceRegistration?>(null);

        return Task.FromResult<DeviceRegistration?>(new DeviceRegistration
        {
            DeviceId = new DeviceId(Guid.Parse(reader.GetString(0))),
            DisplayName = reader.GetString(1),
            PublicKey = (byte[])reader.GetValue(2),
            RegisteredAt = DateTimeOffset.Parse(reader.GetString(3), System.Globalization.CultureInfo.InvariantCulture),
            DeviceToken = reader.GetString(4),
        });
    }

    public Task<bool> ValidateTokenAsync(DeviceId deviceId, string token, CancellationToken ct = default)
    {
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = "SELECT device_token FROM devices WHERE device_id = @id";
        cmd.Parameters.AddWithValue("@id", deviceId.Value.ToString());

        var storedToken = cmd.ExecuteScalar() as string;
        if (storedToken is null) return Task.FromResult(false);

        var storedBytes = System.Text.Encoding.UTF8.GetBytes(storedToken);
        var providedBytes = System.Text.Encoding.UTF8.GetBytes(token);

        return Task.FromResult(CryptographicOperations.FixedTimeEquals(storedBytes, providedBytes));
    }

    public Task<IReadOnlyList<DeviceRegistration>> ListAllAsync(CancellationToken ct = default)
    {
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = "SELECT device_id, display_name, public_key, registered_at, device_token FROM devices";

        using var reader = cmd.ExecuteReader();
        var results = new List<DeviceRegistration>();
        while (reader.Read())
        {
            results.Add(new DeviceRegistration
            {
                DeviceId = new DeviceId(Guid.Parse(reader.GetString(0))),
                DisplayName = reader.GetString(1),
                PublicKey = (byte[])reader.GetValue(2),
                RegisteredAt = DateTimeOffset.Parse(reader.GetString(3), System.Globalization.CultureInfo.InvariantCulture),
                DeviceToken = reader.GetString(4),
            });
        }
        return Task.FromResult<IReadOnlyList<DeviceRegistration>>(results);
    }

    public Task RemoveDeviceAsync(DeviceId deviceId, CancellationToken ct = default)
    {
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = "DELETE FROM devices WHERE device_id = @id";
        cmd.Parameters.AddWithValue("@id", deviceId.Value.ToString());
        cmd.ExecuteNonQuery();
        return Task.CompletedTask;
    }
}
