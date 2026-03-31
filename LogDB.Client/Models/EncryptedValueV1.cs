using Newtonsoft.Json;

namespace LogDB.Client.Models;

/// <summary>
/// V1 encrypted value envelope.
/// Contains AES-GCM encrypted payload plus per-recipient wrapped DEK entries.
/// </summary>
public class EncryptedValueV1
{
    [JsonProperty("v")]
    public int Version { get; set; } = 1;

    [JsonProperty("field")]
    public string FieldName { get; set; } = string.Empty;

    [JsonProperty("logId")]
    public string LogId { get; set; } = string.Empty;

    [JsonProperty("ts")]
    public string Timestamp { get; set; } = string.Empty;

    /// <summary>
    /// AES-GCM encrypted payload: Base64(nonce || ciphertext || tag).
    /// </summary>
    [JsonProperty("payload")]
    public string Payload { get; set; } = string.Empty;

    /// <summary>
    /// Wrapped DEK entries, one per recipient.
    /// </summary>
    [JsonProperty("keys")]
    public List<WrappedDekEntry> Keys { get; set; } = new();
}

/// <summary>
/// A single wrapped DEK entry for one recipient.
/// </summary>
public class WrappedDekEntry
{
    /// <summary>
    /// Recipient key identifier: Base64(first 16 bytes of SHA-256 of public key).
    /// </summary>
    [JsonProperty("kid")]
    public string KeyId { get; set; } = string.Empty;

    /// <summary>
    /// Ephemeral X25519 public key: Base64(32 bytes).
    /// </summary>
    [JsonProperty("epk")]
    public string EphemeralPublicKey { get; set; } = string.Empty;

    /// <summary>
    /// Wrapped DEK: Base64(nonce || encrypted_dek || tag).
    /// </summary>
    [JsonProperty("dek")]
    public string WrappedDek { get; set; } = string.Empty;
}
