using Azure;
using Azure.Data.Tables;
using Federation.Protocol;

namespace Federation.Storage.AzureTables;

internal sealed class AzureTablesDeviceRepository : IDeviceRepository
{
    private readonly TableClient _table;

    public AzureTablesDeviceRepository(TableServiceClient serviceClient)
    {
        _table = serviceClient.GetTableClient("devices");
        _table.CreateIfNotExists();
    }

    public async Task<DeviceRegistration> RegisterDeviceAsync(
        string displayName, byte[] publicKey, CancellationToken ct)
    {
        var deviceId = new DeviceId(Guid.NewGuid());
        var tokenBytes = Guid.NewGuid().ToByteArray().Concat(Guid.NewGuid().ToByteArray()).ToArray();
        var token = Convert.ToBase64String(tokenBytes);
        var now = DateTimeOffset.UtcNow;

        var entity = new TableEntity("all", deviceId.Value.ToString("N"))
        {
            ["DisplayName"] = displayName,
            ["PublicKey"] = Convert.ToBase64String(publicKey),
            ["RegisteredAt"] = now.ToString("O"),
            ["DeviceToken"] = token
        };

        await _table.AddEntityAsync(entity, ct).ConfigureAwait(false);

        return new DeviceRegistration { DeviceId = deviceId, DisplayName = displayName, PublicKey = publicKey, RegisteredAt = now, DeviceToken = token };
    }

    public async Task<DeviceRegistration?> GetDeviceAsync(DeviceId deviceId, CancellationToken ct)
    {
        try
        {
            var response = await _table.GetEntityAsync<TableEntity>(
                "all", deviceId.Value.ToString("N"), cancellationToken: ct).ConfigureAwait(false);
            return ToRegistration(deviceId, response.Value);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task<bool> ValidateTokenAsync(DeviceId deviceId, string token, CancellationToken ct)
    {
        var registration = await GetDeviceAsync(deviceId, ct).ConfigureAwait(false);
        return registration is not null && registration.DeviceToken == token;
    }

    public async Task RemoveDeviceAsync(DeviceId deviceId, CancellationToken ct)
    {
        try
        {
            await _table.DeleteEntityAsync("all", deviceId.Value.ToString("N"), cancellationToken: ct)
                .ConfigureAwait(false);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            // already gone
        }
    }

    private static DeviceRegistration ToRegistration(DeviceId deviceId, TableEntity e)
    {
        var displayName = e.GetString("DisplayName") ?? string.Empty;
        var publicKey = Convert.FromBase64String(e.GetString("PublicKey") ?? string.Empty);
        var registeredAt = DateTimeOffset.Parse(e.GetString("RegisteredAt") ?? DateTimeOffset.MinValue.ToString("O"));
        var token = e.GetString("DeviceToken") ?? string.Empty;
        return new DeviceRegistration { DeviceId = deviceId, DisplayName = displayName, PublicKey = publicKey, RegisteredAt = registeredAt, DeviceToken = token };
    }
}
