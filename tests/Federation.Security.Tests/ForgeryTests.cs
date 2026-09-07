using System.Text;
using Federation.Cryptography;
using Xunit;

namespace Federation.Security.Tests;

/// <summary>
/// Tests that tampered envelopes are rejected by the cryptographic verification layer.
/// </summary>
public sealed class ForgeryTests
{
    private readonly InMemoryCryptoProvider _crypto = new();

    [Fact]
    public void TamperedCiphertext_SignatureVerificationFails()
    {
        var (publicKey, privateKey) = _crypto.GenerateDeviceKeyPair();
        var originalData = Encoding.UTF8.GetBytes("room:discussion:message:1");
        var ciphertext = Encoding.UTF8.GetBytes("encrypted-data-here");

        // Build full signature input (metadata + ciphertext)
        var signatureInput = new byte[originalData.Length + ciphertext.Length];
        originalData.CopyTo(signatureInput, 0);
        ciphertext.CopyTo(signatureInput, originalData.Length);

        var signature = _crypto.Sign(signatureInput, privateKey);

        // Tamper with the ciphertext
        var tamperedCiphertext = (byte[])ciphertext.Clone();
        tamperedCiphertext[0] ^= 0xFF;

        // Rebuild signature input with tampered ciphertext
        var tamperedInput = new byte[originalData.Length + tamperedCiphertext.Length];
        originalData.CopyTo(tamperedInput, 0);
        tamperedCiphertext.CopyTo(tamperedInput, originalData.Length);

        // Verification should fail
        var isValid = _crypto.Verify(tamperedInput, signature, publicKey);
        Assert.False(isValid);
    }

    [Fact]
    public void TamperedMetadata_SignatureVerificationFails()
    {
        var (publicKey, privateKey) = _crypto.GenerateDeviceKeyPair();
        var metadata = Encoding.UTF8.GetBytes("room:discussion:message:1");
        var ciphertext = Encoding.UTF8.GetBytes("encrypted-data");

        var signatureInput = new byte[metadata.Length + ciphertext.Length];
        metadata.CopyTo(signatureInput, 0);
        ciphertext.CopyTo(signatureInput, metadata.Length);

        var signature = _crypto.Sign(signatureInput, privateKey);

        // Tamper with metadata (different room)
        var tamperedMetadata = Encoding.UTF8.GetBytes("room:discussion:message:2");
        var tamperedInput = new byte[tamperedMetadata.Length + ciphertext.Length];
        tamperedMetadata.CopyTo(tamperedInput, 0);
        ciphertext.CopyTo(tamperedInput, tamperedMetadata.Length);

        var isValid = _crypto.Verify(tamperedInput, signature, publicKey);
        Assert.False(isValid);
    }

    [Fact]
    public void ValidSignature_VerificationSucceeds()
    {
        var (publicKey, privateKey) = _crypto.GenerateDeviceKeyPair();
        var data = Encoding.UTF8.GetBytes("room:discussion:message:1:encrypted-payload");

        var signature = _crypto.Sign(data, privateKey);
        var isValid = _crypto.Verify(data, signature, publicKey);

        Assert.True(isValid);
    }

    [Fact]
    public void WrongSignerKey_VerificationFails()
    {
        var (_, privateKey) = _crypto.GenerateDeviceKeyPair();
        var (differentPublicKey, _) = _crypto.GenerateDeviceKeyPair();

        var data = Encoding.UTF8.GetBytes("room:discussion:message:1:payload");
        var signature = _crypto.Sign(data, privateKey);

        // Verify with a different device's public key
        var isValid = _crypto.Verify(data, signature, differentPublicKey);
        Assert.False(isValid);
    }
}
