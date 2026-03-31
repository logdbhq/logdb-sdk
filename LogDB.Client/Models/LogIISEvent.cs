using LogDB.Client.Services;

namespace LogDB.Client.Models;

/// <summary>
/// Represents an IIS access log entry to be sent to LogDB
/// </summary>
public class LogIISEvent
{
    internal string? ApiKey { get; set; }
    internal bool _IsEncrypted { get; set; }

    public LogIISEvent() { }

    public LogIISEvent(string? apiKey, string guid)
    {
        ApiKey = apiKey;
        Guid = guid;
    }

    public string Guid { get; set; } = System.Guid.NewGuid().ToString();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? Collection { get; set; }

    // Request info
    public string? Method { get; set; }
    public string? UriStem { get; set; }
    public string? UriQuery { get; set; }
    public int? Port { get; set; }
    public string? Username { get; set; }
    public string? Host { get; set; }

    // Client/Server
    public string? ClientIp { get; set; }
    public string? ServerIp { get; set; }
    public string? UserAgent { get; set; }
    public string? Referer { get; set; }

    // Response
    public int? Status { get; set; }
    public int? SubStatus { get; set; }
    public int? Win32Status { get; set; }
    public int? TimeTaken { get; set; }
    public long? BytesSent { get; set; }
    public long? BytesReceived { get; set; }

    // Server identification
    public string? SiteName { get; set; }
    public string? ServerName { get; set; }

    /// <summary>
    /// Converts to a Log entry with _sys_type=iis_event for server-side routing
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
            Application = SiteName ?? string.Empty,
            Message = $"{Method} {UriStem} {Status}",
            Level = Status >= 500 ? LogLevel.Error : Status >= 400 ? LogLevel.Warning : LogLevel.Info,
            Source = "iis",
            IpAddress = ClientIp,
            StatusCode = Status,
            RequestPath = UriStem,
            HttpMethod = Method,
        };

        log.AttributesS["_sys_type"] = "iis_event";

        if (!string.IsNullOrEmpty(Method))
            log.AttributesS["method"] = Method;
        if (!string.IsNullOrEmpty(UriStem))
            log.AttributesS["uriStem"] = UriStem;
        if (!string.IsNullOrEmpty(UriQuery))
            log.AttributesS["queryString"] = UriQuery;
        if (!string.IsNullOrEmpty(Username))
            log.AttributesS["username"] = Username;
        if (!string.IsNullOrEmpty(ClientIp))
            log.AttributesS["clientIp"] = ClientIp;
        if (!string.IsNullOrEmpty(ServerIp))
            log.AttributesS["serverIp"] = ServerIp;
        if (!string.IsNullOrEmpty(UserAgent))
            log.AttributesS["userAgent"] = UserAgent;
        if (!string.IsNullOrEmpty(Referer))
            log.AttributesS["referer"] = Referer;
        if (!string.IsNullOrEmpty(Host))
            log.AttributesS["host"] = Host;
        if (!string.IsNullOrEmpty(SiteName))
            log.AttributesS["siteName"] = SiteName;
        if (!string.IsNullOrEmpty(ServerName))
            log.AttributesS["serverName"] = ServerName;

        if (Status.HasValue) log.AttributesN["statusCode"] = Status.Value;
        if (SubStatus.HasValue) log.AttributesN["subStatus"] = SubStatus.Value;
        if (Win32Status.HasValue) log.AttributesN["win32Status"] = Win32Status.Value;
        if (TimeTaken.HasValue) log.AttributesN["timeTaken"] = TimeTaken.Value;
        if (Port.HasValue) log.AttributesN["serverPort"] = Port.Value;
        if (BytesSent.HasValue) log.AttributesN["bytesSent"] = BytesSent.Value;
        if (BytesReceived.HasValue) log.AttributesN["bytesReceived"] = BytesReceived.Value;

        return log;
    }

    private void EncryptFields()
    {
        if (!string.IsNullOrEmpty(UriQuery) && !UriQuery.StartsWith("encrypted_by_logdb"))
            UriQuery = EncryptionService.Encrypt(UriQuery);
        if (!string.IsNullOrEmpty(Username) && !Username.StartsWith("encrypted_by_logdb"))
            Username = EncryptionService.Encrypt(Username);
        if (!string.IsNullOrEmpty(ClientIp) && !ClientIp.StartsWith("encrypted_by_logdb"))
            ClientIp = EncryptionService.Encrypt(ClientIp);
        if (!string.IsNullOrEmpty(ServerIp) && !ServerIp.StartsWith("encrypted_by_logdb"))
            ServerIp = EncryptionService.Encrypt(ServerIp);
        if (!string.IsNullOrEmpty(UserAgent) && !UserAgent.StartsWith("encrypted_by_logdb"))
            UserAgent = EncryptionService.Encrypt(UserAgent);
        if (!string.IsNullOrEmpty(Referer) && !Referer.StartsWith("encrypted_by_logdb"))
            Referer = EncryptionService.Encrypt(Referer);
    }
}
