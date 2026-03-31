using System.Security.Cryptography;
using LogDB.Client.Models;
using LogDB.Client.Services;
using LogDB.Client.Services.Crypto;
using Newtonsoft.Json;
using Xunit;

namespace LogDB.Client.Tests.Crypto;

public class EncryptionServiceV1Tests
{
    private const string FieldName = "UserEmail";
    private const string LogId = "log-abc-123";
    private const string Timestamp = "2026-02-26T10:30:00Z";
    private const string Plaintext = "user@example.com";

    [Fact]
    public void Roundtrip_SingleRecipient()
    {
        var (privateKey, publicKey) = KeyWrapping.GenerateX25519KeyPair();

        var envelope = EncryptionServiceV1.Encrypt(
            Plaintext, FieldName, LogId, Timestamp, new[] { publicKey });

        Assert.Equal(1, envelope.Version);
        Assert.Single(envelope.Keys);
        Assert.Equal(FieldName, envelope.FieldName);
        Assert.Equal(LogId, envelope.LogId);
        Assert.Equal(Timestamp, envelope.Timestamp);

        var decrypted = EncryptionServiceV1.Decrypt(
            envelope, privateKey, publicKey, FieldName, LogId, Timestamp);
        Assert.Equal(Plaintext, decrypted);
    }

    [Fact]
    public void Roundtrip_MultipleRecipients()
    {
        var (privKey1, pubKey1) = KeyWrapping.GenerateX25519KeyPair();
        var (privKey2, pubKey2) = KeyWrapping.GenerateX25519KeyPair();
        var (privKey3, pubKey3) = KeyWrapping.GenerateX25519KeyPair();

        var envelope = EncryptionServiceV1.Encrypt(
            Plaintext, FieldName, LogId, Timestamp,
            new[] { pubKey1, pubKey2, pubKey3 });

        Assert.Equal(3, envelope.Keys.Count);

        Assert.Equal(Plaintext, EncryptionServiceV1.Decrypt(
            envelope, privKey1, pubKey1, FieldName, LogId, Timestamp));
        Assert.Equal(Plaintext, EncryptionServiceV1.Decrypt(
            envelope, privKey2, pubKey2, FieldName, LogId, Timestamp));
        Assert.Equal(Plaintext, EncryptionServiceV1.Decrypt(
            envelope, privKey3, pubKey3, FieldName, LogId, Timestamp));
    }

    [Fact]
    public void TamperedCiphertext_DecryptionFails()
    {
        var (privateKey, publicKey) = KeyWrapping.GenerateX25519KeyPair();

        var envelope = EncryptionServiceV1.Encrypt(
            Plaintext, FieldName, LogId, Timestamp, new[] { publicKey });

        var payloadBytes = Convert.FromBase64String(envelope.Payload);
        payloadBytes[AesGcmHelper.NonceSizeBytes] ^= 0xFF;
        envelope.Payload = Convert.ToBase64String(payloadBytes);

        Assert.Throws<AuthenticationTagMismatchException>(() =>
            EncryptionServiceV1.Decrypt(
                envelope, privateKey, publicKey, FieldName, LogId, Timestamp));
    }

    [Fact]
    public void TamperedWrappedDek_UnwrapFails()
    {
        var (privateKey, publicKey) = KeyWrapping.GenerateX25519KeyPair();

        var envelope = EncryptionServiceV1.Encrypt(
            Plaintext, FieldName, LogId, Timestamp, new[] { publicKey });

        var dekBytes = Convert.FromBase64String(envelope.Keys[0].WrappedDek);
        dekBytes[AesGcmHelper.NonceSizeBytes] ^= 0xFF;
        envelope.Keys[0].WrappedDek = Convert.ToBase64String(dekBytes);

        Assert.Throws<AuthenticationTagMismatchException>(() =>
            EncryptionServiceV1.Decrypt(
                envelope, privateKey, publicKey, FieldName, LogId, Timestamp));
    }

    [Fact]
    public void WrongRecipient_CannotDecrypt()
    {
        var (_, pubKey) = KeyWrapping.GenerateX25519KeyPair();
        var (wrongPriv, wrongPub) = KeyWrapping.GenerateX25519KeyPair();

        var envelope = EncryptionServiceV1.Encrypt(
            Plaintext, FieldName, LogId, Timestamp, new[] { pubKey });

        Assert.ThrowsAny<CryptographicException>(() =>
            EncryptionServiceV1.Decrypt(
                envelope, wrongPriv, wrongPub, FieldName, LogId, Timestamp));
    }

    [Fact]
    public void MismatchedCallerContext_ThrowsReplayDetection()
    {
        var (privateKey, publicKey) = KeyWrapping.GenerateX25519KeyPair();

        var envelope = EncryptionServiceV1.Encrypt(
            Plaintext, FieldName, LogId, Timestamp, new[] { publicKey });

        // Caller supplies different expected field — replay detection kicks in
        Assert.Throws<CryptographicException>(() =>
            EncryptionServiceV1.Decrypt(
                envelope, privateKey, publicKey, "DifferentField", LogId, Timestamp));
    }

    [Fact]
    public void TamperedEnvelopeContext_ThrowsReplayDetection()
    {
        var (privateKey, publicKey) = KeyWrapping.GenerateX25519KeyPair();

        var envelope = EncryptionServiceV1.Encrypt(
            Plaintext, FieldName, LogId, Timestamp, new[] { publicKey });

        // Attacker mutates envelope context to match a different record
        envelope.FieldName = "DifferentField";

        // Caller supplies the original expected context — mismatch detected
        Assert.Throws<CryptographicException>(() =>
            EncryptionServiceV1.Decrypt(
                envelope, privateKey, publicKey, FieldName, LogId, Timestamp));
    }

    [Fact]
    public void Serialize_Deserialize_Roundtrip()
    {
        var (privateKey, publicKey) = KeyWrapping.GenerateX25519KeyPair();

        var envelope = EncryptionServiceV1.Encrypt(
            Plaintext, FieldName, LogId, Timestamp, new[] { publicKey });

        var serialized = EncryptionServiceV1.Serialize(envelope);
        Assert.StartsWith(EncryptionServiceV1.Prefix, serialized);

        var deserialized = EncryptionServiceV1.Deserialize(serialized);

        var decrypted = EncryptionServiceV1.Decrypt(
            deserialized, privateKey, publicKey, FieldName, LogId, Timestamp);
        Assert.Equal(Plaintext, decrypted);
    }

    [Fact]
    public void Envelope_SerializesToValidJson()
    {
        var (_, publicKey) = KeyWrapping.GenerateX25519KeyPair();

        var envelope = EncryptionServiceV1.Encrypt(
            Plaintext, FieldName, LogId, Timestamp, new[] { publicKey });

        var json = JsonConvert.SerializeObject(envelope);
        var parsed = JsonConvert.DeserializeObject<EncryptedValueV1>(json);

        Assert.NotNull(parsed);
        Assert.Equal(1, parsed.Version);
        Assert.NotEmpty(parsed.Payload);
        Assert.Single(parsed.Keys);
    }

    [Fact]
    public void EmptyPlaintext_Throws()
    {
        var (_, publicKey) = KeyWrapping.GenerateX25519KeyPair();

        Assert.Throws<ArgumentException>(() =>
            EncryptionServiceV1.Encrypt("", FieldName, LogId, Timestamp, new[] { publicKey }));
    }

    [Fact]
    public void NoRecipients_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            EncryptionServiceV1.Encrypt(Plaintext, FieldName, LogId, Timestamp,
                Array.Empty<byte[]>()));
    }
}
