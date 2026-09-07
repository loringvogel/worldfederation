using Federation.Protocol;

namespace Federation.Cryptography;

/// <summary>
/// Provides device-level cryptographic operations.
/// Production implementations should use a reviewed MLS library.
/// </summary>
public interface ICryptoProvider
{
    /// <summary>Generates a new ECDSA P-256 key pair for a device. Returns (publicKey, privateKey).</summary>
    (byte[] PublicKey, byte[] PrivateKey) GenerateDeviceKeyPair();

    /// <summary>Signs data with the given private key.</summary>
    byte[] Sign(ReadOnlySpan<byte> data, byte[] privateKey);

    /// <summary>Verifies a signature against data and a public key.</summary>
    bool Verify(ReadOnlySpan<byte> data, ReadOnlySpan<byte> signature, byte[] publicKey);
}

/// <summary>
/// Manages group encryption sessions with epoch tracking.
/// Production implementations should use a reviewed MLS library.
/// </summary>
public interface IGroupSession
{
    /// <summary>The current cryptographic epoch for this group.</summary>
    EpochId CurrentEpoch { get; }

    /// <summary>Encrypts plaintext for the group. Returns (ciphertext, epoch at time of encryption).</summary>
    (byte[] Ciphertext, EpochId Epoch) EncryptMessage(ReadOnlySpan<byte> plaintext, ReadOnlySpan<byte> additionalData);

    /// <summary>Decrypts ciphertext from the group, validating epoch.</summary>
    byte[] DecryptMessage(ReadOnlySpan<byte> ciphertext, ReadOnlySpan<byte> additionalData, EpochId epoch);

    /// <summary>Adds a member to the group. Does NOT rotate epoch (only removal triggers rotation).</summary>
    void AddMember(DeviceId deviceId, byte[] publicKey);

    /// <summary>Removes a member from the group and rotates the epoch.</summary>
    void RemoveMember(DeviceId deviceId);
}

/// <summary>
/// Persists cryptographic key material for a device.
/// Production implementations should use hardware-backed storage (TPM, HSM, or OS keychain).
/// </summary>
public interface IKeyStore
{
    Task StoreDeviceKeyAsync(DeviceId deviceId, byte[] privateKey, CancellationToken ct = default);
    Task<byte[]?> LoadDeviceKeyAsync(DeviceId deviceId, CancellationToken ct = default);
    Task DeleteEpochKeyAsync(EpochId epoch, CancellationToken ct = default);
}
