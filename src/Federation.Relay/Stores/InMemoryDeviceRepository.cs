using System.Collections.Concurrent;
using Federation.Protocol;

namespace Federation.Relay.Stores;

public sealed class InMemoryDeviceRepository : IDeviceRepository
{
    private readonly ConcurrentDictionary<DeviceId, DeviceRegistration> _devices = new();

    public Task<DeviceRegistration> RegisterDeviceAsync(string displayName, byte[] publicKey, CancellationToken ct = default)
    {
        var deviceId = DeviceId.New();
        var token = Guid.CreateVersion7().ToString("N");
        var registration = new DeviceRegistration { DeviceId = deviceId, DisplayName = displayName, PublicKey = publicKey, RegisteredAt = DateTimeOffset.UtcNow, DeviceToken = token };
        if (!_devices.TryAdd(deviceId, registration))
            throw new InvalidOperationException("Failed to register device (ID collision).");
        return Task.FromResult(registration);
    }

    public Task<DeviceRegistration?> GetDeviceAsync(DeviceId deviceId, CancellationToken ct = default)
    {
        _devices.TryGetValue(deviceId, out var device);
        return Task.FromResult(device);
    }

    public Task<bool> ValidateTokenAsync(DeviceId deviceId, string token, CancellationToken ct = default)
    {
        if (_devices.TryGetValue(deviceId, out var device))
            return Task.FromResult(string.Equals(device.DeviceToken, token, StringComparison.Ordinal));
        return Task.FromResult(false);
    }

    public Task RemoveDeviceAsync(DeviceId deviceId, CancellationToken ct = default)
    {
        _devices.TryRemove(deviceId, out _);
        return Task.CompletedTask;
    }
}
