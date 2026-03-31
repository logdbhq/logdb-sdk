namespace LogDB.Client.Models;

/// <summary>
/// Key-value metadata for tags and fields
/// </summary>
public class LogMeta
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
