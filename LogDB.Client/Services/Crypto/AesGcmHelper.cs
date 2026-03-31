using System.Security.Cryptography;

namespace LogDB.Client.Services.Crypto;

/// <summary>
/// Low-level AES-256-GCM authenticated encryption helpers.
/// </summary>
internal static class AesGcmHelper
{
    public const int KeySizeBytes = 32;   // 256-bit
    public const int NonceSizeBytes = 12; // 96-bit
    public const int TagSizeBytes = 16;   // 128-bit

    /// <summary>
    /// Result of an AES-GCM encryption operation.
    /// </summary>
    public readonly record struct AesGcmBlob(byte[] Nonce, byte[] Ciphertext, byte[] Tag)
    {
        /// <summary>
        /// Serialize to nonce || ciphertext || tag.
        /// </summary>
        public byte[] ToBytes()
        {
            var result = new byte[Nonce.Length + Ciphertext.Length + Tag.Length];
            Nonce.CopyTo(result, 0);
            Ciphertext.CopyTo(result, Nonce.Length);
            Tag.CopyTo(result, Nonce.Length + Ciphertext.Length);
            return result;
        }

        /// <summary>
        /// Deserialize from nonce || ciphertext || tag (ciphertext length inferred).
        /// </summary>
        public static AesGcmBlob FromBytes(ReadOnlySpan<byte> data)
        {
            if (data.Length < NonceSizeBytes + TagSizeBytes)
                throw new ArgumentException("AES-GCM blob is too short.");

            int ciphertextLength = data.Length - NonceSizeBytes - TagSizeBytes;
            var nonce = data[..NonceSizeBytes].ToArray();
            var ciphertext = data.Slice(NonceSizeBytes, ciphertextLength).ToArray();
            var tag = data.Slice(NonceSizeBytes + ciphertextLength, TagSizeBytes).ToArray();
            return new AesGcmBlob(nonce, ciphertext, tag);
        }
    }

    /// <summary>
    /// Encrypt plaintext with AES-256-GCM. Generates a random 12-byte nonce.
    /// </summary>
    public static AesGcmBlob Encrypt(
        ReadOnlySpan<byte> key,
        ReadOnlySpan<byte> plaintext,
        ReadOnlySpan<byte> associatedData = default)
    {
        if (key.Length != KeySizeBytes)
            throw new ArgumentException($"Key must be {KeySizeBytes} bytes.", nameof(key));

        var nonce = new byte[NonceSizeBytes];
        RandomNumberGenerator.Fill(nonce);

        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagSizeBytes];

        using var aesGcm = new AesGcm(key, TagSizeBytes);
        aesGcm.Encrypt(nonce, plaintext, ciphertext, tag, associatedData);

        return new AesGcmBlob(nonce, ciphertext, tag);
    }

    /// <summary>
    /// Decrypt an AES-GCM blob.
    /// </summary>
    /// <exception cref="AuthenticationTagMismatchException">Tampered data or wrong key.</exception>
    public static byte[] Decrypt(
        ReadOnlySpan<byte> key,
        AesGcmBlob blob,
        ReadOnlySpan<byte> associatedData = default)
    {
        if (key.Length != KeySizeBytes)
            throw new ArgumentException($"Key must be {KeySizeBytes} bytes.", nameof(key));

        var plaintext = new byte[blob.Ciphertext.Length];

        using var aesGcm = new AesGcm(key, TagSizeBytes);
        aesGcm.Decrypt(blob.Nonce, blob.Ciphertext, blob.Tag, plaintext, associatedData);

        return plaintext;
    }

    /// <summary>
    /// Generate a random 256-bit key.
    /// </summary>
    public static byte[] GenerateKey()
    {
        var key = new byte[KeySizeBytes];
        RandomNumberGenerator.Fill(key);
        return key;
    }
}
