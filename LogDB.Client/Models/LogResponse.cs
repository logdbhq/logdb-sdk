namespace LogDB.Client.Models;

/// <summary>
/// Response from log operations
/// </summary>
public class LogResponse
{
    public LogResponseStatus Status { get; set; }
    public string? Message { get; set; }
}
