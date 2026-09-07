using System.Security.Cryptography;
using System.Text;
using Federation.Cryptography;
using Federation.Protocol;
using Xunit;

namespace Federation.Cryptography.Tests;

public sealed class InMemoryGroupSessionTests
{
    [Fact]
    public void EncryptDecrypt_RoundTrip_Succeeds()
    {
        var session = new InMemoryGroupSession();
        var plaintext = Encoding.UTF8.GetBytes("Proposal: We should adopt policy X.");
        var aad = Encoding.UTF8.GetBytes("room:discussion:message");

        var (ciphertext, epoch) = session.EncryptMessage(plaintext, aad);
        var decrypted = session.DecryptMessage(ciphertext, aad, epoch);

        Assert.Equal(plaintext, decrypted);
    }

    [Fact]
    public void Decrypt_WrongEpoch_Throws()
    {
        var session = new InMemoryGroupSession();
        var plaintext = Encoding.UTF8.GetBytes("Test message");
        var aad = Encoding.UTF8.GetBytes("aad");

        var (ciphertext, _) = session.EncryptMessage(plaintext, aad);

        // Try to decrypt with wrong epoch
        var wrongEpoch = new EpochId(999);
        Assert.Throws<CryptographicException>(() =>
            session.DecryptMessage(ciphertext, aad, wrongEpoch));
    }

    [Fact]
    public void AddMember_DoesNotChangeEpoch()
    {
        var session = new InMemoryGroupSession();
        var epochBefore = session.CurrentEpoch;

        session.AddMember(DeviceId.New(), new byte[] { 1, 2, 3 });

        Assert.Equal(epochBefore, session.CurrentEpoch);
    }

    [Fact]
    public void RemoveMember_IncrementsEpoch()
    {
        var session = new InMemoryGroupSession();
        var deviceId = DeviceId.New();
        session.AddMember(deviceId, new byte[] { 1, 2, 3 });

        var epochBefore = session.CurrentEpoch;
        session.RemoveMember(deviceId);

        Assert.Equal(epochBefore.Value + 1, session.CurrentEpoch.Value);
    }

    [Fact]
    public void RemoveMember_OldEpochCiphertext_CannotDecrypt()
    {
        var session = new InMemoryGroupSession();
        var deviceId = DeviceId.New();
        session.AddMember(deviceId, new byte[] { 1, 2, 3 });

        var plaintext = Encoding.UTF8.GetBytes("Secret message");
        var aad = Encoding.UTF8.GetBytes("aad");
        var (ciphertext, epochAtEncrypt) = session.EncryptMessage(plaintext, aad);

        // Remove member (rotates epoch and key)
        session.RemoveMember(deviceId);

        // Attempting to decrypt with old epoch should fail
        Assert.Throws<CryptographicException>(() =>
            session.DecryptMessage(ciphertext, aad, epochAtEncrypt));
    }

    [Fact]
    public void RemoveNonMember_Throws()
    {
        var session = new InMemoryGroupSession();

        Assert.Throws<InvalidOperationException>(() =>
            session.RemoveMember(DeviceId.New()));
    }

    [Fact]
    public void InitialEpoch_IsOne()
    {
        var session = new InMemoryGroupSession();
        Assert.Equal(1L, session.CurrentEpoch.Value);
    }

    [Fact]
    public void EncryptedCiphertext_DiffersFromPlaintext()
    {
        var session = new InMemoryGroupSession();
        var plaintext = Encoding.UTF8.GetBytes("This is plaintext");
        var aad = Encoding.UTF8.GetBytes("aad");

        var (ciphertext, _) = session.EncryptMessage(plaintext, aad);

        // Ciphertext should not contain the plaintext bytes
        var plaintextStr = Encoding.UTF8.GetString(plaintext);
        var ciphertextStr = Encoding.UTF8.GetString(ciphertext);
        Assert.DoesNotContain(plaintextStr, ciphertextStr);
    }

    [Fact]
    public void TwoSessions_SharedKey_CanDecryptEachOther()
    {
        var session1 = new InMemoryGroupSession();
        var session2 = new InMemoryGroupSession();

        // Synchronize session2 with session1's key
        session2.SetGroupKey(session1.GetGroupKey(), session1.CurrentEpoch.Value);

        var plaintext = Encoding.UTF8.GetBytes("Cross-session message");
        var aad = Encoding.UTF8.GetBytes("aad");

        var (ciphertext, epoch) = session1.EncryptMessage(plaintext, aad);
        var decrypted = session2.DecryptMessage(ciphertext, aad, epoch);

        Assert.Equal(plaintext, decrypted);
    }
}
