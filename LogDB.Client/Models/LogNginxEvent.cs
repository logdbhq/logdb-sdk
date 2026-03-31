using LogDB.Client.Services;

namespace LogDB.Client.Models;

/// <summary>
/// Represents an Nginx access or error log entry to be sent to LogDB
/// </summary>
public class LogNginxEvent
{
    internal string? ApiKey { get; set; }
    internal bool _IsEncrypted { get; set; }

    public LogNginxEvent() { }

    public LogNginxEvent(string? apiKey, string guid)
    {
        ApiKey = apiKey;
        Guid = guid;
    }

    public string Guid { get; set; } = System.Guid.NewGuid().ToString();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? Collection { get; set; }

    /// <summary>
    /// "access" or "error"
    /// </summary>
    public string LogType { get; set; } = "access";

    public string? TargetName { get; set; }
    public string? HostName { get; set; }
    public string? SourceFile { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Level { get; set; } = "Info";

    // HTTP request fields
    public string? RemoteAddress { get; set; }
    public string? Method { get; set; }
    public string? Path { get; set; }
    public string? Protocol { get; set; }
    public int? StatusCode { get; set; }
    public long? ResponseBytes { get; set; }
    public string? Referer { get; set; }
    public string? UserAgent { get; set; }
    public double? RequestTime { get; set; }

    // Server info
    public string? ServerName { get; set; }
    public string? Severity { get; set; }
    public int? Pid { get; set; }
    public int? Tid { get; set; }
    public long? ConnectionId { get; set; }
    public string? Upstream { get; set; }

    /// <summary>
    /// Converts to a Log entry with _sys_type=nginx_event for server-side routing
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
            Application = TargetName ?? string.Empty,
            Message = Message,
            Level = ParseLevel(Level),
            Source = "nginx",
            IpAddress = RemoteAddress,
            StatusCode = StatusCode,
            RequestPath = Path,
            HttpMethod = Method,
        };

        log.AttributesS["_sys_type"] = "nginx_event";

        if (!string.IsNullOrEmpty(LogType))
            log.AttributesS["nginx.log_type"] = LogType;
        if (!string.IsNullOrEmpty(TargetName))
            log.AttributesS["nginx.target"] = TargetName;
        if (!string.IsNullOrEmpty(HostName))
            log.AttributesS["host.name"] = HostName;
        if (!string.IsNullOrEmpty(SourceFile))
            log.AttributesS["nginx.source_file"] = SourceFile;
        if (!string.IsNullOrEmpty(RemoteAddress))
            log.AttributesS["nginx.remote_addr"] = RemoteAddress;
        if (!string.IsNullOrEmpty(Method))
            log.AttributesS["http.method"] = Method;
        if (!string.IsNullOrEmpty(Path))
            log.AttributesS["http.path"] = Path;
        if (!string.IsNullOrEmpty(Protocol))
            log.AttributesS["http.protocol"] = Protocol;
        if (!string.IsNullOrEmpty(Referer))
            log.AttributesS["http.referer"] = Referer;
        if (!string.IsNullOrEmpty(UserAgent))
            log.AttributesS["http.user_agent"] = UserAgent;
        if (!string.IsNullOrEmpty(ServerName))
            log.AttributesS["nginx.server_name"] = ServerName;
        if (!string.IsNullOrEmpty(Severity))
            log.AttributesS["nginx.severity"] = Severity;
        if (!string.IsNullOrEmpty(Upstream))
            log.AttributesS["nginx.upstream"] = Upstream;

        if (StatusCode.HasValue) log.AttributesN["http.status_code"] = StatusCode.Value;
        if (ResponseBytes.HasValue) log.AttributesN["http.response_bytes"] = ResponseBytes.Value;
        if (RequestTime.HasValue) log.AttributesN["http.request_time"] = RequestTime.Value;
        if (Pid.HasValue) log.AttributesN["nginx.pid"] = Pid.Value;
        if (Tid.HasValue) log.AttributesN["nginx.tid"] = Tid.Value;
        if (ConnectionId.HasValue) log.AttributesN["nginx.connection_id"] = ConnectionId.Value;

        return log;
    }

    private void EncryptFields()
    {
        if (!string.IsNullOrEmpty(Message) && !Message.StartsWith("encrypted_by_logdb"))
            Message = EncryptionService.Encrypt(Message);
        if (!string.IsNullOrEmpty(RemoteAddress) && !RemoteAddress.StartsWith("encrypted_by_logdb"))
            RemoteAddress = EncryptionService.Encrypt(RemoteAddress);
        if (!string.IsNullOrEmpty(UserAgent) && !UserAgent.StartsWith("encrypted_by_logdb"))
            UserAgent = EncryptionService.Encrypt(UserAgent);
        if (!string.IsNullOrEmpty(Referer) && !Referer.StartsWith("encrypted_by_logdb"))
            Referer = EncryptionService.Encrypt(Referer);
    }

    private static LogLevel ParseLevel(string level)
    {
        return level?.ToLowerInvariant() switch
        {
            "trace" or "debug" => LogLevel.Debug,
            "info" or "information" or "notice" => LogLevel.Info,
            "warning" or "warn" => LogLevel.Warning,
            "error" or "err" => LogLevel.Error,
            "critical" or "crit" or "alert" or "emerg" => LogLevel.Critical,
            _ => LogLevel.Info
        };
    }
}
