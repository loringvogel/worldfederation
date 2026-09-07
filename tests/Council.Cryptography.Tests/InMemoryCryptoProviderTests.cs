using System.Text;
using Xunit;

namespace Council.Cryptography.Tests;

public sealed class InMemoryCryptoProviderTests
{
    private readonly InMemoryCryptoProvider _crypto = new();

    [Fact]
    public void GenerateDeviceKeyPair_ReturnsNonEmptyKeys()
    {
        var (publicKey, privateKey) = _crypto.GenerateDeviceKeyPair();

        Assert.NotEmpty(publicKey);
        Assert.NotEmpty(privateKey);
    }

    [Fact]
    public void SignAndVerify_RoundTrip_Succeeds()
    {
        var (publicKey, privateKey) = _crypto.GenerateDeviceKeyPair();
        var data = Encoding.UTF8.GetBytes("Hello, council!");

        var signature = _crypto.Sign(data, privateKey);
        var isValid = _crypto.Verify(data, signature, publicKey);

        Assert.True(isValid);
    }

    [Fact]
    public void Verify_TamperedData_Fails()
    {
        var (publicKey, privateKey) = _crypto.GenerateDeviceKeyPair();
        var data = Encoding.UTF8.GetBytes("Hello, council!");

        var signature = _crypto.Sign(data, privateKey);

        var tamperedData = Encoding.UTF8.GetBytes("Hello, tampered!");
        var isValid = _crypto.Verify(tamperedData, signature, publicKey);

        Assert.False(isValid);
    }

    [Fact]
    public void Verify_TamperedSignature_Fails()
    {
        var (publicKey, privateKey) = _crypto.GenerateDeviceKeyPair();
        var data = Encoding.UTF8.GetBytes("Hello, council!");

        var signature = _crypto.Sign(data, privateKey);

        // Tamper with the signature
        var tamperedSig = (byte[])signature.Clone();
        tamperedSig[0] ^= 0xFF;

        var isValid = _crypto.Verify(data, tamperedSig, publicKey);

        Assert.False(isValid);
    }

    [Fact]
    public void Verify_WrongPublicKey_Fails()
    {
        var (_, privateKey) = _crypto.GenerateDeviceKeyPair();
        var (wrongPublicKey, _) = _crypto.GenerateDeviceKeyPair();
        var data = Encoding.UTF8.GetBytes("Hello, council!");

        var signature = _crypto.Sign(data, privateKey);
        var isValid = _crypto.Verify(data, signature, wrongPublicKey);

        Assert.False(isValid);
    }

    [Fact]
    public void GenerateDeviceKeyPair_ProducesDifferentKeysEachTime()
    {
        var (pub1, priv1) = _crypto.GenerateDeviceKeyPair();
        var (pub2, priv2) = _crypto.GenerateDeviceKeyPair();

        Assert.NotEqual(pub1, pub2);
        Assert.NotEqual(priv1, priv2);
    }
}
