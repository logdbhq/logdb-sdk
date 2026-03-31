namespace LogDB.Client.Models;

/// <summary>
/// Represents a key-value cache entry to be sent to LogDB
/// </summary>
public class LogCache
{
    // Internal - set by client, not exposed to consumers
    internal string? ApiKey { get; set; }
    internal bool _IsEncrypted { get; set; }

    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Guid { get; set; } = System.Guid.NewGuid().ToString();
    public DateTime? Timestamp { get; set; }
    public string? Collection { get; set; }
    public int? TtlSeconds { get; set; }
}
