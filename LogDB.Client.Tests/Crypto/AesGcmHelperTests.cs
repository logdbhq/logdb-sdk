using System.Security.Cryptography;
using LogDB.Client.Services.Crypto;
using Xunit;

namespace LogDB.Client.Tests.Crypto;

public class AesGcmHelperTests
{
    [Fact]
    public void Encrypt_Decrypt_Roundtrip()
    {
        var key = AesGcmHelper.GenerateKey();
        var plaintext = "Hello, LogDB encryption!"u8.ToArray();
        var aad = "context"u8.ToArray();

        var blob = AesGcmHelper.Encrypt(key, plaintext, aad);
        var decrypted = AesGcmHelper.Decrypt(key, blob, aad);

        Assert.Equal(plaintext, decrypted);
    }

    [Fact]
    public void Encrypt_Decrypt_WithoutAad_Roundtrips()
    {
        var key = AesGcmHelper.GenerateKey();
        var plaintext = "No AAD test"u8.ToArray();

        var blob = AesGcmHelper.Encrypt(key, plaintext);
        var decrypted = AesGcmHelper.Decrypt(key, blob);

        Assert.Equal(plaintext, decrypted);
    }

    [Fact]
    public void TamperedCiphertext_Throws()
    {
        var key = AesGcmHelper.GenerateKey();
        var plaintext = "Tamper test"u8.ToArray();

        var blob = AesGcmHelper.Encrypt(key, plaintext);
        blob.Ciphertext[0] ^= 0xFF;

        Assert.Throws<AuthenticationTagMismatchException>(() =>
            AesGcmHelper.Decrypt(key, blob));
    }

    [Fact]
    public void TamperedTag_Throws()
    {
        var key = AesGcmHelper.GenerateKey();
        var plaintext = "Tag tamper"u8.ToArray();

        var blob = AesGcmHelper.Encrypt(key, plaintext);
        blob.Tag[0] ^= 0xFF;

        Assert.Throws<AuthenticationTagMismatchException>(() =>
            AesGcmHelper.Decrypt(key, blob));
    }

    [Fact]
    public void WrongKey_Throws()
    {
        var key1 = AesGcmHelper.GenerateKey();
        var key2 = AesGcmHelper.GenerateKey();
        var plaintext = "Wrong key"u8.ToArray();

        var blob = AesGcmHelper.Encrypt(key1, plaintext);

        Assert.Throws<AuthenticationTagMismatchException>(() =>
            AesGcmHelper.Decrypt(key2, blob));
    }

    [Fact]
    public void WrongAad_Throws()
    {
        var key = AesGcmHelper.GenerateKey();
        var plaintext = "AAD mismatch"u8.ToArray();

        var blob = AesGcmHelper.Encrypt(key, plaintext, "correct"u8);

        Assert.Throws<AuthenticationTagMismatchException>(() =>
            AesGcmHelper.Decrypt(key, blob, "wrong"u8));
    }

    [Fact]
    public void Blob_ToBytes_FromBytes_Roundtrip()
    {
        var key = AesGcmHelper.GenerateKey();
        var plaintext = "Serialization"u8.ToArray();

        var blob = AesGcmHelper.Encrypt(key, plaintext);
        var bytes = blob.ToBytes();
        var restored = AesGcmHelper.AesGcmBlob.FromBytes(bytes);

        Assert.Equal(blob.Nonce, restored.Nonce);
        Assert.Equal(blob.Ciphertext, restored.Ciphertext);
        Assert.Equal(blob.Tag, restored.Tag);
    }

    [Fact]
    public void GenerateKey_Returns32Bytes()
    {
        var key = AesGcmHelper.GenerateKey();
        Assert.Equal(32, key.Length);
    }

    [Fact]
    public void TwoCalls_ProduceDifferentNonces()
    {
        var key = AesGcmHelper.GenerateKey();
        var plaintext = "Nonce uniqueness"u8.ToArray();

        var blob1 = AesGcmHelper.Encrypt(key, plaintext);
        var blob2 = AesGcmHelper.Encrypt(key, plaintext);

        Assert.NotEqual(blob1.Nonce, blob2.Nonce);
    }
}
