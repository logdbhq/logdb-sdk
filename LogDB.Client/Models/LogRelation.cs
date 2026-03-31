namespace LogDB.Client.Models;

/// <summary>
/// Represents a graph relationship between entities to be sent to LogDB
/// </summary>
public class LogRelation
{
    // Internal - set by client, not exposed to consumers
    internal string? ApiKey { get; set; }
    internal string? CustomerId { get; set; }

    public string Origin { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Relation { get; set; } = string.Empty;
    public string Guid { get; set; } = System.Guid.NewGuid().ToString();
    public DateTime? DateIn { get; set; }
    public string? Collection { get; set; }
    public string? Environment { get; set; }
    public string? Application { get; set; }

    public Dictionary<string, object>? OriginProperties { get; set; }
    public Dictionary<string, object>? SubjectProperties { get; set; }
    public Dictionary<string, object>? RelationProperties { get; set; }
}
