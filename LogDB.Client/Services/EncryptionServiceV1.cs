using System.Security.Cryptography;
using System.Text;
using LogDB.Client.Models;
using LogDB.Client.Services.Crypto;
using Newtonsoft.Json;

namespace LogDB.Client.Services;

/// <summary>
/// V1 field encryption service using AES-256-GCM + X25519 key wrapping.
/// </summary>
public static class EncryptionServiceV1
{
    public const string Prefix = "encrypted_v1:";

    /// <summary>
    /// Generate a new X25519 key pair for V1 encryption.
    /// Returns (privateKey, publicKey) as raw 32-byte arrays.
    /// Store the private key securely; share the public key with log producers.
    /// </summary>
    public static (byte[] PrivateKey, byte[] PublicKey) GenerateKeyPair()
        => KeyWrapping.GenerateX25519KeyPair();

    /// <summary>
    /// Encrypt a plaintext string for one or more recipient X25519 public keys.
    /// </summary>
    public static EncryptedValueV1 Encrypt(
        string plaintext,
        string fieldName,
        string logId,
        string timestamp,
        IReadOnlyList<byte[]> recipientPublicKeys)
    {
        if (string.IsNullOrEmpty(plaintext))
            throw new ArgumentException("Plaintext cannot be null or empty.", nameof(plaintext));
        if (recipientPublicKeys == null || recipientPublicKeys.Count == 0)
            throw new ArgumentException("At least one recipient public key is required.",
                nameof(recipientPublicKeys));

        var dek = AesGcmHelper.GenerateKey();

        try
        {
            var aad = BuildAad(fieldName, logId, timestamp);
            var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
            var encryptedPayload = AesGcmHelper.Encrypt(dek, plaintextBytes, aad);

            var wrappedKeys = new List<WrappedDekEntry>(recipientPublicKeys.Count);
            foreach (var recipientPubKey in recipientPublicKeys)
            {
                var wrapped = KeyWrapping.WrapDek(dek, recipientPubKey);
                wrappedKeys.Add(new WrappedDekEntry
                {
                    KeyId = Convert.ToBase64String(wrapped.RecipientPublicKeyId),
                    EphemeralPublicKey = Convert.ToBase64String(wrapped.EphemeralPublicKey),
                    WrappedDek = Convert.ToBase64String(wrapped.EncryptedDek.ToBytes())
                });
            }

            return new EncryptedValueV1
            {
                Version = 1,
                FieldName = fieldName,
                LogId = logId,
                Timestamp = timestamp,
                Payload = Convert.ToBase64String(encryptedPayload.ToBytes()),
                Keys = wrappedKeys
            };
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dek);
        }
    }

    /// <summary>
    /// Serialize an EncryptedValueV1 to the prefixed wire format.
    /// </summary>
    public static string Serialize(EncryptedValueV1 envelope)
    {
        var json = JsonConvert.SerializeObject(envelope, Formatting.None);
        return Prefix + Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
    }

    /// <summary>
    /// Deserialize a prefixed string back to EncryptedValueV1.
    /// </summary>
    public static EncryptedValueV1 Deserialize(string encoded)
    {
        if (!encoded.StartsWith(Prefix))
            throw new ArgumentException("Not a V1 encrypted value.", nameof(encoded));

        var base64 = encoded[Prefix.Length..];
        var json = Encoding.UTF8.GetString(Convert.FromBase64String(base64));
        return JsonConvert.DeserializeObject<EncryptedValueV1>(json)
            ?? throw new InvalidOperationException("Failed to deserialize EncryptedValueV1.");
    }

    /// <summary>
    /// Decrypt an EncryptedValueV1 using the recipient's X25519 key pair.
    /// Caller must supply the expected context (fieldName, logId, timestamp) to
    /// prevent replay of an envelope into a different record/field context.
    /// </summary>
    public static string Decrypt(
        EncryptedValueV1 envelope,
        ReadOnlySpan<byte> recipientPrivateKey,
        ReadOnlySpan<byte> recipientPublicKey,
        string expectedFieldName,
        string expectedLogId,
        string expectedTimestamp)
    {
        if (envelope.Version != 1)
            throw new NotSupportedException($"Unsupported encryption version: {envelope.Version}");

        if (envelope.FieldName != expectedFieldName ||
            envelope.LogId != expectedLogId ||
            envelope.Timestamp != expectedTimestamp)
        {
            throw new CryptographicException(
                "Envelope context does not match expected context. Possible replay.");
        }

        var ourKeyId = Convert.ToBase64String(KeyWrapping.ComputeKeyId(recipientPublicKey));

        var entry = envelope.Keys.FirstOrDefault(k => k.KeyId == ourKeyId)
            ?? throw new CryptographicException(
                "No wrapped DEK found for the provided recipient key.");

        var wrappedDekBytes = Convert.FromBase64String(entry.WrappedDek);
        var ephemeralPubKeyBytes = Convert.FromBase64String(entry.EphemeralPublicKey);
        var recipientKeyIdBytes = Convert.FromBase64String(entry.KeyId);

        var wrappedKey = new KeyWrapping.WrappedKey(
            recipientKeyIdBytes,
            ephemeralPubKeyBytes,
            AesGcmHelper.AesGcmBlob.FromBytes(wrappedDekBytes));

        var dek = KeyWrapping.UnwrapDek(recipientPrivateKey, wrappedKey);

        try
        {
            var aad = BuildAad(expectedFieldName, expectedLogId, expectedTimestamp);
            var payloadBytes = Convert.FromBase64String(envelope.Payload);
            var payloadBlob = AesGcmHelper.AesGcmBlob.FromBytes(payloadBytes);
            var plaintext = AesGcmHelper.Decrypt(dek, payloadBlob, aad);

            return Encoding.UTF8.GetString(plaintext);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dek);
        }
    }

    /// <summary>
    /// Build AAD using length-prefixed encoding to avoid delimiter ambiguity.
    /// Format: [2-byte BE len][utf8 fieldName][2-byte BE len][utf8 logId][2-byte BE len][utf8 timestamp]
    /// </summary>
    internal static byte[] BuildAad(string fieldName, string logId, string timestamp)
    {
        var f = Encoding.UTF8.GetBytes(fieldName);
        var l = Encoding.UTF8.GetBytes(logId);
        var t = Encoding.UTF8.GetBytes(timestamp);

        var aad = new byte[2 + f.Length + 2 + l.Length + 2 + t.Length];
        var span = aad.AsSpan();
        int offset = 0;

        WriteSegment(span, ref offset, f);
        WriteSegment(span, ref offset, l);
        WriteSegment(span, ref offset, t);

        return aad;

        static void WriteSegment(Span<byte> dest, ref int pos, byte[] data)
        {
            if (data.Length > ushort.MaxValue)
                throw new ArgumentException("AAD segment exceeds maximum length of 65535 bytes.");
            System.Buffers.Binary.BinaryPrimitives.WriteUInt16BigEndian(dest[pos..], (ushort)data.Length);
            pos += 2;
            data.CopyTo(dest[pos..]);
            pos += data.Length;
        }
    }
}
