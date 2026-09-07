using System.Security.Cryptography;
using Federation.Protocol;

namespace Federation.Cryptography;

/// <summary>
/// DEVELOPMENT STUB ONLY. Uses AES-GCM for message encryption with epoch as a simple counter.
/// Replace with a reviewed MLS implementation before production.
///
/// This stub uses a single shared symmetric key that is re-derived on epoch rotation.
/// Real MLS provides forward secrecy, post-compromise security, and proper key scheduling.
/// </summary>
public sealed class InMemoryGroupSession : IGroupSession
{
    private readonly object _lock = new();
    private readonly Dictionary<DeviceId, byte[]> _members = [];
    private byte[] _groupKey;
    private long _epoch;

    public InMemoryGroupSession()
    {
        _groupKey = RandomNumberGenerator.GetBytes(32);
        _epoch = 1;
    }

    /// <inheritdoc />
    public EpochId CurrentEpoch
    {
        get
        {
            lock (_lock)
            {
                return new EpochId(_epoch);
            }
        }
    }

    /// <inheritdoc />
    public (byte[] Ciphertext, EpochId Epoch) EncryptMessage(ReadOnlySpan<byte> plaintext, ReadOnlySpan<byte> additionalData)
    {
        lock (_lock)
        {
            var nonce = RandomNumberGenerator.GetBytes(AesGcm.NonceByteSizes.MaxSize);
            var ciphertext = new byte[plaintext.Length];
            var tag = new byte[AesGcm.TagByteSizes.MaxSize];

            using var aes = new AesGcm(_groupKey, AesGcm.TagByteSizes.MaxSize);
            aes.Encrypt(nonce, plaintext, ciphertext, tag, additionalData);

            // Pack as: [nonce_len(1)][nonce][tag_len(1)][tag][ciphertext]
            var result = new byte[1 + nonce.Length + 1 + tag.Length + ciphertext.Length];
            result[0] = (byte)nonce.Length;
            nonce.CopyTo(result.AsSpan(1));
            result[1 + nonce.Length] = (byte)tag.Length;
            tag.CopyTo(result.AsSpan(2 + nonce.Length));
            ciphertext.CopyTo(result.AsSpan(2 + nonce.Length + tag.Length));

            return (result, new EpochId(_epoch));
        }
    }

    /// <inheritdoc />
    public byte[] DecryptMessage(ReadOnlySpan<byte> ciphertext, ReadOnlySpan<byte> additionalData, EpochId epoch)
    {
        lock (_lock)
        {
            if (epoch.Value != _epoch)
            {
                throw new CryptographicException($"Epoch mismatch: message epoch {epoch.Value}, current epoch {_epoch}.");
            }

            // Unpack: [nonce_len(1)][nonce][tag_len(1)][tag][ciphertext]
            var nonceLen = ciphertext[0];
            var nonce = ciphertext.Slice(1, nonceLen);
            var tagLen = ciphertext[1 + nonceLen];
            var tag = ciphertext.Slice(2 + nonceLen, tagLen);
            var encryptedData = ciphertext[(2 + nonceLen + tagLen)..];

            var plaintext = new byte[encryptedData.Length];

            using var aes = new AesGcm(_groupKey, AesGcm.TagByteSizes.MaxSize);
            aes.Decrypt(nonce, encryptedData, tag, plaintext, additionalData);

            return plaintext;
        }
    }

    /// <inheritdoc />
    public void AddMember(DeviceId deviceId, byte[] publicKey)
    {
        lock (_lock)
        {
            _members[deviceId] = publicKey;
            // Note: Adding a member does NOT rotate the epoch.
            // The new member receives the current group key out-of-band in this stub.
        }
    }

    /// <inheritdoc />
    public void RemoveMember(DeviceId deviceId)
    {
        lock (_lock)
        {
            if (!_members.Remove(deviceId))
            {
                throw new InvalidOperationException($"Device '{deviceId}' is not a member of this group.");
            }

            // Rotate epoch: generate a new group key so the removed member cannot decrypt future messages.
            _groupKey = RandomNumberGenerator.GetBytes(32);
            _epoch++;
        }
    }

    /// <summary>
    /// Exposes the current group key for testing or for sharing with newly added members.
    /// DEVELOPMENT STUB ONLY: real MLS handles key distribution through the protocol.
    /// </summary>
    public byte[] GetGroupKey()
    {
        lock (_lock)
        {
            return (byte[])_groupKey.Clone();
        }
    }

    /// <summary>
    /// Sets the group key directly, used to synchronize multiple InMemoryGroupSession instances
    /// in local simulation. DEVELOPMENT STUB ONLY.
    /// </summary>
    public void SetGroupKey(byte[] key, long epoch)
    {
        ArgumentNullException.ThrowIfNull(key);

        lock (_lock)
        {
            _groupKey = (byte[])key.Clone();
            _epoch = epoch;
        }
    }
}
