using System.Collections.Concurrent;
using Federation.Protocol;

namespace Federation.Cryptography;

/// <summary>
/// DEVELOPMENT STUB ONLY. Stores key material in an in-memory dictionary.
/// Offers NO persistence, NO hardware protection, NO encryption at rest.
/// Replace with hardware-backed key storage (TPM, HSM, or OS keychain) before production.
/// </summary>
public sealed class InMemoryKeyStore : IKeyStore
{
    private readonly ConcurrentDictionary<DeviceId, byte[]> _deviceKeys = new();
    private readonly ConcurrentDictionary<EpochId, byte[]> _epochKeys = new();

    /// <inheritdoc />
    public Task StoreDeviceKeyAsync(DeviceId deviceId, byte[] privateKey, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(privateKey);
        _deviceKeys[deviceId] = (byte[])privateKey.Clone();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<byte[]?> LoadDeviceKeyAsync(DeviceId deviceId, CancellationToken ct = default)
    {
        var result = _deviceKeys.TryGetValue(deviceId, out var key) ? (byte[])key.Clone() : null;
        return Task.FromResult(result);
    }

    /// <inheritdoc />
    public Task DeleteEpochKeyAsync(EpochId epoch, CancellationToken ct = default)
    {
        _epochKeys.TryRemove(epoch, out _);
        return Task.CompletedTask;
    }

    /// <summary>Stores an epoch key. Used by the stub group session for key distribution.</summary>
    public Task StoreEpochKeyAsync(EpochId epoch, byte[] key, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        _epochKeys[epoch] = (byte[])key.Clone();
        return Task.CompletedTask;
    }

    /// <summary>Loads an epoch key.</summary>
    public Task<byte[]?> LoadEpochKeyAsync(EpochId epoch, CancellationToken ct = default)
    {
        var result = _epochKeys.TryGetValue(epoch, out var key) ? (byte[])key.Clone() : null;
        return Task.FromResult(result);
    }
}
