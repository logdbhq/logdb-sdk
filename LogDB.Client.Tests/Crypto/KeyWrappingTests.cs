using System.Security.Cryptography;
using LogDB.Client.Services.Crypto;
using Xunit;

namespace LogDB.Client.Tests.Crypto;

public class KeyWrappingTests
{
    [Fact]
    public void WrapDek_UnwrapDek_Roundtrip()
    {
        var (privateKey, publicKey) = KeyWrapping.GenerateX25519KeyPair();
        var dek = AesGcmHelper.GenerateKey();

        var wrapped = KeyWrapping.WrapDek(dek, publicKey);
        var unwrapped = KeyWrapping.UnwrapDek(privateKey, wrapped);

        Assert.Equal(dek, unwrapped);
    }

    [Fact]
    public void WrongPrivateKey_ThrowsOnUnwrap()
    {
        var (_, publicKey) = KeyWrapping.GenerateX25519KeyPair();
        var (wrongPrivateKey, _) = KeyWrapping.GenerateX25519KeyPair();
        var dek = AesGcmHelper.GenerateKey();

        var wrapped = KeyWrapping.WrapDek(dek, publicKey);

        Assert.Throws<AuthenticationTagMismatchException>(() =>
            KeyWrapping.UnwrapDek(wrongPrivateKey, wrapped));
    }

    [Fact]
    public void TamperedEncryptedDek_ThrowsOnUnwrap()
    {
        var (privateKey, publicKey) = KeyWrapping.GenerateX25519KeyPair();
        var dek = AesGcmHelper.GenerateKey();

        var wrapped = KeyWrapping.WrapDek(dek, publicKey);
        wrapped.EncryptedDek.Ciphertext[0] ^= 0xFF;

        Assert.Throws<AuthenticationTagMismatchException>(() =>
            KeyWrapping.UnwrapDek(privateKey, wrapped));
    }

    [Fact]
    public void GenerateX25519KeyPair_CorrectSizes()
    {
        var (privateKey, publicKey) = KeyWrapping.GenerateX25519KeyPair();

        Assert.Equal(32, privateKey.Length);
        Assert.Equal(32, publicKey.Length);
    }

    [Fact]
    public void ComputeKeyId_Returns16Bytes()
    {
        var (_, publicKey) = KeyWrapping.GenerateX25519KeyPair();
        var keyId = KeyWrapping.ComputeKeyId(publicKey);

        Assert.Equal(16, keyId.Length);
    }

    [Fact]
    public void ComputeKeyId_Deterministic()
    {
        var (_, publicKey) = KeyWrapping.GenerateX25519KeyPair();
        var id1 = KeyWrapping.ComputeKeyId(publicKey);
        var id2 = KeyWrapping.ComputeKeyId(publicKey);

        Assert.Equal(id1, id2);
    }

    [Fact]
    public void ComputeKeyId_DifferentKeys_DifferentIds()
    {
        var (_, pk1) = KeyWrapping.GenerateX25519KeyPair();
        var (_, pk2) = KeyWrapping.GenerateX25519KeyPair();

        Assert.NotEqual(KeyWrapping.ComputeKeyId(pk1), KeyWrapping.ComputeKeyId(pk2));
    }
}
