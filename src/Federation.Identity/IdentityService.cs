using System.Text;
using System.Text.Json;
using Federation.Cryptography;
using Federation.Protocol;

namespace Federation.Identity;

/// <summary>
/// Creates and manages local device identity using ICryptoProvider.
/// Signs peer records and verifies peer record signatures.
/// </summary>
public sealed class IdentityService
{
    private readonly ICryptoProvider _crypto;
    private readonly IIdentityStore _store;

    public IdentityService(ICryptoProvider crypto, IIdentityStore store)
    {
        ArgumentNullException.ThrowIfNull(crypto);
        ArgumentNullException.ThrowIfNull(store);
        _crypto = crypto;
        _store = store;
    }

    /// <summary>Creates a new device identity and stores it.</summary>
    public async Task<DeviceIdentity> CreateDeviceIdentityAsync(string displayName, CancellationToken ct = default)
    {
        var (signingPublicKey, _) = _crypto.GenerateDeviceKeyPair();
        var (agreementPublicKey, _) = _crypto.GenerateDeviceKeyPair();

        var identity = new DeviceIdentity
        {
            Id = DeviceId.New(),
            DisplayName = displayName,
            PublicSigningKey = signingPublicKey,
            PublicKeyAgreementKey = agreementPublicKey,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        await _store.StoreDeviceIdentityAsync(identity, ct).ConfigureAwait(false);
        return identity;
    }

    /// <summary>Signs a peer record with the given private key.</summary>
    public PeerRecord SignPeerRecord(PeerRecord unsignedRecord, byte[] privateKey)
    {
        ArgumentNullException.ThrowIfNull(unsignedRecord);
        ArgumentNullException.ThrowIfNull(privateKey);

        var dataToSign = GetPeerRecordSignatureInput(unsignedRecord);
        var signature = _crypto.Sign(dataToSign, privateKey);

        return unsignedRecord with { Signature = signature };
    }

    /// <summary>Verifies a peer record's signature using the device's public signing key.</summary>
    public bool VerifyPeerRecord(PeerRecord record, byte[] publicSigningKey)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(publicSigningKey);

        var dataToVerify = GetPeerRecordSignatureInput(record);
        return _crypto.Verify(dataToVerify, record.Signature, publicSigningKey);
    }

    private static byte[] GetPeerRecordSignatureInput(PeerRecord record)
    {
        var payload = $"{record.DeviceId}:{record.HumanId}:{string.Join(",", record.Addresses)}:{record.IssuedAt:O}:{record.ExpiresAt:O}";
        return Encoding.UTF8.GetBytes(payload);
    }
}
