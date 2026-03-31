using System.Security.Cryptography;
using System.Text;
using NSec.Cryptography;

namespace LogDB.Client.Services.Crypto;

/// <summary>
/// X25519 + HKDF-SHA256 + AES-GCM key wrapping for encrypting a DEK
/// to one or more recipients' X25519 public keys.
/// </summary>
internal static class KeyWrapping
{
    private static readonly KeyAgreementAlgorithm X25519 =
        KeyAgreementAlgorithm.X25519;

    private static readonly KeyDerivationAlgorithm HkdfSha256 =
        KeyDerivationAlgorithm.HkdfSha256;

    private static readonly byte[] HkdfInfoPrefix =
        Encoding.UTF8.GetBytes("LogDB.V1.KeyWrap");

    /// <summary>
    /// Result of wrapping a DEK for a single recipient.
    /// </summary>
    public readonly record struct WrappedKey(
        byte[] RecipientPublicKeyId,
        byte[] EphemeralPublicKey,
        AesGcmHelper.AesGcmBlob EncryptedDek);

    /// <summary>
    /// Wrap a DEK for a single recipient X25519 public key.
    /// </summary>
    public static WrappedKey WrapDek(
        ReadOnlySpan<byte> dek,
        ReadOnlySpan<byte> recipientPublicKeyBytes)
    {
        if (dek.Length != AesGcmHelper.KeySizeBytes)
            throw new ArgumentException("DEK must be 32 bytes.", nameof(dek));

        var recipientPubKey = NSec.Cryptography.PublicKey.Import(
            X25519, recipientPublicKeyBytes, KeyBlobFormat.RawPublicKey);

        using var ephemeralKey = Key.Create(X25519,
            new KeyCreationParameters { ExportPolicy = KeyExportPolicies.AllowPlaintextExport });

        using var sharedSecret = X25519.Agree(ephemeralKey, recipientPubKey)
            ?? throw new CryptographicException("X25519 key agreement failed.");

        var ephemeralPubKeyBytes = ephemeralKey.PublicKey.Export(KeyBlobFormat.RawPublicKey);
        var recipientKeyId = ComputeKeyId(recipientPublicKeyBytes);
        var wrappingKey = DeriveWrappingKey(sharedSecret, ephemeralPubKeyBytes, recipientKeyId);

        try
        {
            var encryptedDek = AesGcmHelper.Encrypt(wrappingKey, dek);
            return new WrappedKey(recipientKeyId, ephemeralPubKeyBytes, encryptedDek);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(wrappingKey);
        }
    }

    /// <summary>
    /// Unwrap a DEK using the recipient's X25519 private key.
    /// </summary>
    public static byte[] UnwrapDek(
        ReadOnlySpan<byte> recipientPrivateKeyBytes,
        WrappedKey wrapped)
    {
        using var recipientKey = Key.Import(X25519,
            recipientPrivateKeyBytes, KeyBlobFormat.RawPrivateKey,
            new KeyCreationParameters { ExportPolicy = KeyExportPolicies.AllowPlaintextExport });

        var ephemeralPubKey = NSec.Cryptography.PublicKey.Import(
            X25519, wrapped.EphemeralPublicKey, KeyBlobFormat.RawPublicKey);

        using var sharedSecret = X25519.Agree(recipientKey, ephemeralPubKey)
            ?? throw new CryptographicException("X25519 key agreement failed during unwrap.");

        var wrappingKey = DeriveWrappingKey(sharedSecret, wrapped.EphemeralPublicKey, wrapped.RecipientPublicKeyId);

        try
        {
            return AesGcmHelper.Decrypt(wrappingKey, wrapped.EncryptedDek);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(wrappingKey);
        }
    }

    /// <summary>
    /// Generate a new X25519 keypair. Returns (privateKey, publicKey) as raw 32-byte arrays.
    /// </summary>
    public static (byte[] PrivateKey, byte[] PublicKey) GenerateX25519KeyPair()
    {
        using var key = Key.Create(X25519,
            new KeyCreationParameters { ExportPolicy = KeyExportPolicies.AllowPlaintextExport });

        var privateKeyBytes = key.Export(KeyBlobFormat.RawPrivateKey);
        var publicKeyBytes = key.PublicKey.Export(KeyBlobFormat.RawPublicKey);

        return (privateKeyBytes, publicKeyBytes);
    }

    public const int KeyIdSizeBytes = 16;

    /// <summary>
    /// Key identifier: first 16 bytes (128-bit) of SHA-256 of the public key.
    /// </summary>
    public static byte[] ComputeKeyId(ReadOnlySpan<byte> publicKey)
    {
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(publicKey, hash);
        return hash[..KeyIdSizeBytes].ToArray();
    }

    /// <summary>
    /// Derive a 256-bit wrapping key from a shared secret using HKDF-SHA256.
    /// Info = "LogDB.V1.KeyWrap" || epk || recipientKeyId for domain separation.
    /// </summary>
    private static byte[] DeriveWrappingKey(
        SharedSecret sharedSecret,
        ReadOnlySpan<byte> ephemeralPublicKey,
        ReadOnlySpan<byte> recipientKeyId)
    {
        var info = new byte[HkdfInfoPrefix.Length + ephemeralPublicKey.Length + recipientKeyId.Length];
        HkdfInfoPrefix.CopyTo(info, 0);
        ephemeralPublicKey.CopyTo(info.AsSpan(HkdfInfoPrefix.Length));
        recipientKeyId.CopyTo(info.AsSpan(HkdfInfoPrefix.Length + ephemeralPublicKey.Length));

        return HkdfSha256.DeriveBytes(
            sharedSecret,
            salt: ReadOnlySpan<byte>.Empty,
            info: info,
            count: AesGcmHelper.KeySizeBytes);
    }
}
