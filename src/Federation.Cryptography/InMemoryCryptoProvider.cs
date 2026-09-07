using System.Security.Cryptography;

namespace Federation.Cryptography;

/// <summary>
/// DEVELOPMENT STUB ONLY. Uses ECDSA P-256 for signing via System.Security.Cryptography.
/// Replace with a reviewed MLS implementation before production.
/// This stub is suitable for local simulation and testing only.
/// </summary>
public sealed class InMemoryCryptoProvider : ICryptoProvider
{
    /// <inheritdoc />
    public (byte[] PublicKey, byte[] PrivateKey) GenerateDeviceKeyPair()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var privateKey = ecdsa.ExportECPrivateKey();
        var publicKey = ecdsa.ExportSubjectPublicKeyInfo();
        return (publicKey, privateKey);
    }

    /// <inheritdoc />
    public byte[] Sign(ReadOnlySpan<byte> data, byte[] privateKey)
    {
        using var ecdsa = ECDsa.Create();
        ecdsa.ImportECPrivateKey(privateKey, out _);
        return ecdsa.SignData(data.ToArray(), HashAlgorithmName.SHA256);
    }

    /// <inheritdoc />
    public bool Verify(ReadOnlySpan<byte> data, ReadOnlySpan<byte> signature, byte[] publicKey)
    {
        using var ecdsa = ECDsa.Create();
        ecdsa.ImportSubjectPublicKeyInfo(publicKey, out _);
        return ecdsa.VerifyData(data.ToArray(), signature.ToArray(), HashAlgorithmName.SHA256);
    }
}
