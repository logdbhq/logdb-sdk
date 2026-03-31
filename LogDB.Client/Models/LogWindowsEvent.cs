using LogDB.Client.Services;

namespace LogDB.Client.Models;

/// <summary>
/// Represents a Windows Event Log entry to be sent to LogDB
/// </summary>
public class LogWindowsEvent
{
    internal string? ApiKey { get; set; }
    internal bool _IsEncrypted { get; set; }

    public LogWindowsEvent() { }

    public LogWindowsEvent(string? apiKey, string guid)
    {
        ApiKey = apiKey;
        Guid = guid;
    }

    public string Guid { get; set; } = System.Guid.NewGuid().ToString();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? Collection { get; set; }
    public string? Environment { get; set; }
    public string? Application { get; set; }

    public long? EventId { get; set; }
    public string? ProviderName { get; set; }
    public string? Channel { get; set; }
    public string? Task { get; set; }
    public string? Opcode { get; set; }
    public string Level { get; set; } = "Information";
    public string? Keywords { get; set; }
    public string? Computer { get; set; }
    public string? UserId { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? XmlData { get; set; }
    public string? IpAddress { get; set; }

    /// <summary>
    /// Converts to a Log entry with _sys_type=windows_event for server-side routing
    /// </summary>
    public Log ToLog()
    {
        if (_IsEncrypted)
            EncryptFields();

        var log = new Log
        {
            ApiKey = ApiKey,
            Guid = Guid,
            Timestamp = Timestamp,
            Collection = Collection,
            Environment = Environment ?? string.Empty,
            Application = Application ?? string.Empty,
            Message = Message,
            Level = MapLevel(Level),
            Source = ProviderName,
            IpAddress = IpAddress,
        };

        log.AttributesS["_sys_type"] = "windows_event";

        if (EventId.HasValue)
            log.AttributesS["eventId"] = EventId.Value.ToString();
        if (!string.IsNullOrEmpty(Channel))
            log.AttributesS["channel"] = Channel;
        if (!string.IsNullOrEmpty(Task))
            log.AttributesS["task"] = Task;
        if (!string.IsNullOrEmpty(Opcode))
            log.AttributesS["opcode"] = Opcode;
        if (!string.IsNullOrEmpty(Keywords))
            log.AttributesS["keywords"] = Keywords;
        if (!string.IsNullOrEmpty(Computer))
            log.AttributesS["computer"] = Computer;
        if (!string.IsNullOrEmpty(UserId))
            log.AttributesS["userId"] = UserId;
        if (!string.IsNullOrEmpty(XmlData))
            log.AttributesS["xmlDetails"] = XmlData;

        return log;
    }

    private void EncryptFields()
    {
        if (!string.IsNullOrEmpty(Message) && !Message.StartsWith("encrypted_by_logdb"))
            Message = EncryptionService.Encrypt(Message);
        if (!string.IsNullOrEmpty(Computer) && !Computer.StartsWith("encrypted_by_logdb"))
            Computer = EncryptionService.Encrypt(Computer);
        if (!string.IsNullOrEmpty(UserId) && !UserId.StartsWith("encrypted_by_logdb"))
            UserId = EncryptionService.Encrypt(UserId);
        if (!string.IsNullOrEmpty(IpAddress) && !IpAddress.StartsWith("encrypted_by_logdb"))
            IpAddress = EncryptionService.Encrypt(IpAddress);
        if (!string.IsNullOrEmpty(XmlData) && !XmlData.StartsWith("encrypted_by_logdb"))
            XmlData = EncryptionService.Encrypt(XmlData);
    }

    private static LogLevel MapLevel(string level)
    {
        return level?.ToLowerInvariant() switch
        {
            "critical" or "1" => LogLevel.Critical,
            "error" or "2" => LogLevel.Error,
            "warning" or "3" => LogLevel.Warning,
            "information" or "info" or "4" => LogLevel.Info,
            "verbose" or "debug" or "5" => LogLevel.Debug,
            _ => LogLevel.Info
        };
    }
}
