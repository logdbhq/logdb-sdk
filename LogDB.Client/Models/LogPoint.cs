namespace LogDB.Client.Models;

/// <summary>
/// Represents a metrics/time-series data point to be sent to LogDB
/// </summary>
public class LogPoint
{
    // Internal - set by client, not exposed to consumers
    internal string? ApiKey { get; set; }

    public string Guid { get; set; } = System.Guid.NewGuid().ToString();
    public string Measurement { get; set; } = string.Empty;
    public List<LogMeta> Tag { get; set; } = new List<LogMeta>();
    public List<LogMeta> Field { get; set; } = new List<LogMeta>();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? Collection { get; set; }

    public string? Environment
    {
        get => Tag.FirstOrDefault(d => d.Key.Equals("environment"))?.Value;
        set
        {
            if (value != null)
                Tag.Add(new LogMeta { Key = "environment", Value = value });
        }
    }

    public string? Application
    {
        get => Tag.FirstOrDefault(d => d.Key.Equals("application"))?.Value;
        set
        {
            if (value != null)
                Tag.Add(new LogMeta { Key = "application", Value = value });
        }
    }
}
