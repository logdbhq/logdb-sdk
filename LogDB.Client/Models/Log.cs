namespace LogDB.Client.Models;

/// <summary>
/// Represents a log entry to be sent to LogDB
/// </summary>
public class Log
{
    // Internal - set by client, not exposed to consumers
    internal string? ApiKey { get; set; }
    internal bool _IsEncrypted { get; set; }
    internal long? ID { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Application { get; set; } = string.Empty;
    public string Environment { get; set; } = string.Empty;
    public LogLevel Level { get; set; } = LogLevel.Info;
    public string Message { get; set; } = string.Empty;
    public string? Exception { get; set; }
    public string? StackTrace { get; set; }
    public string? Source { get; set; }
    public int? UserId { get; set; }
    public string? UserEmail { get; set; }
    public string? CorrelationId { get; set; }
    public string? RequestPath { get; set; }
    public string? HttpMethod { get; set; }
    public string? AdditionalData { get; set; }
    public string? IpAddress { get; set; }
    public int? StatusCode { get; set; }
    public string? Description { get; set; }
    public string Guid { get; set; } = System.Guid.NewGuid().ToString();
    public string? Collection { get; set; }

    public List<string> Label { get; init; } = new List<string>();
    public Dictionary<string, string> AttributesS { get; init; } = new Dictionary<string, string>();
    public Dictionary<string, double> AttributesN { get; init; } = new Dictionary<string, double>();
    public Dictionary<string, bool> AttributesB { get; init; } = new Dictionary<string, bool>();
    public Dictionary<string, DateTime> AttributesD { get; init; } = new Dictionary<string, DateTime>();
}
