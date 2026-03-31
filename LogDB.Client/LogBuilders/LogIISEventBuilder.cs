using com.logdb.LogDB;
using LogDB.Client.Models;
using LogDB.Client.Services;

namespace com.logdb.logger.LogBuilders;

public sealed class LogIISEventBuilder
{
    private static LoggerContext _context = new();
    private readonly LogIISEvent _entry;
    private static Logger? _logger;

    private LogIISEventBuilder(LogIISEvent entry)
    {
        _entry = entry;
    }

    public static string? Collection { get; set; }

    public static string ApiKey
    {
        get => _context.ApiKey;
        set
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("ApiKey cannot be null or empty.", nameof(value));
            _context.ApiKey = value;
        }
    }

    private static Logger GetOrCreateLogger()
    {
        if (string.IsNullOrWhiteSpace(_context.ApiKey))
            throw new InvalidOperationException("LogIISEventBuilder.ApiKey must be set before logging.");

        return _logger ??= Logger.Create(_context.ApiKey);
    }

    public static LogIISEventBuilder Create()
    {
        if (string.IsNullOrWhiteSpace(_context.ApiKey))
            throw new InvalidOperationException("LogIISEventBuilder.ApiKey must be set before Create().");

        return new LogIISEventBuilder(new LogIISEvent(_context.ApiKey, Guid.NewGuid().ToString())
        {
            Timestamp = DateTime.UtcNow
        });
    }

    public LogIISEvent Build() => _entry;

    private static LogIISEvent CloneEntry(LogIISEvent o)
    {
        return new LogIISEvent(o.ApiKey, o.Guid)
        {
            Collection = o.Collection,
            Timestamp = o.Timestamp,
            Method = o.Method,
            UriStem = o.UriStem,
            UriQuery = o.UriQuery,
            Port = o.Port,
            Username = o.Username,
            Host = o.Host,
            ClientIp = o.ClientIp,
            ServerIp = o.ServerIp,
            UserAgent = o.UserAgent,
            Referer = o.Referer,
            Status = o.Status,
            SubStatus = o.SubStatus,
            Win32Status = o.Win32Status,
            TimeTaken = o.TimeTaken,
            BytesSent = o.BytesSent,
            BytesReceived = o.BytesReceived,
            SiteName = o.SiteName,
            ServerName = o.ServerName
        };
    }

    public LogIISEventBuilder SetTimestamp(DateTime timestamp)
    {
        var n = CloneEntry(_entry); n.Timestamp = timestamp; return new(n);
    }

    public LogIISEventBuilder SetCollection(string? collection)
    {
        var n = CloneEntry(_entry); n.Collection = collection; return new(n);
    }

    public LogIISEventBuilder SetMethod(string method)
    {
        var n = CloneEntry(_entry); n.Method = method; return new(n);
    }

    public LogIISEventBuilder SetUriStem(string uriStem)
    {
        var n = CloneEntry(_entry); n.UriStem = uriStem; return new(n);
    }

    public LogIISEventBuilder SetUriQuery(string uriQuery, bool isEncrypted = false)
    {
        var n = CloneEntry(_entry); n.UriQuery = isEncrypted ? EncryptionService.Encrypt(uriQuery) : uriQuery; return new(n);
    }

    public LogIISEventBuilder SetPort(int port)
    {
        var n = CloneEntry(_entry); n.Port = port; return new(n);
    }

    public LogIISEventBuilder SetUsername(string username, bool isEncrypted = false)
    {
        var n = CloneEntry(_entry); n.Username = isEncrypted ? EncryptionService.Encrypt(username) : username; return new(n);
    }

    public LogIISEventBuilder SetHost(string host)
    {
        var n = CloneEntry(_entry); n.Host = host; return new(n);
    }

    public LogIISEventBuilder SetClientIp(string clientIp, bool isEncrypted = false)
    {
        var n = CloneEntry(_entry); n.ClientIp = isEncrypted ? EncryptionService.Encrypt(clientIp) : clientIp; return new(n);
    }

    public LogIISEventBuilder SetServerIp(string serverIp, bool isEncrypted = false)
    {
        var n = CloneEntry(_entry); n.ServerIp = isEncrypted ? EncryptionService.Encrypt(serverIp) : serverIp; return new(n);
    }

    public LogIISEventBuilder SetUserAgent(string userAgent, bool isEncrypted = false)
    {
        var n = CloneEntry(_entry); n.UserAgent = isEncrypted ? EncryptionService.Encrypt(userAgent) : userAgent; return new(n);
    }

    public LogIISEventBuilder SetReferer(string referer, bool isEncrypted = false)
    {
        var n = CloneEntry(_entry); n.Referer = isEncrypted ? EncryptionService.Encrypt(referer) : referer; return new(n);
    }

    public LogIISEventBuilder Encrypt()
    {
        var n = CloneEntry(_entry); n._IsEncrypted = true; return new(n);
    }

    public LogIISEventBuilder SetStatus(int status)
    {
        var n = CloneEntry(_entry); n.Status = status; return new(n);
    }

    public LogIISEventBuilder SetSubStatus(int subStatus)
    {
        var n = CloneEntry(_entry); n.SubStatus = subStatus; return new(n);
    }

    public LogIISEventBuilder SetWin32Status(int win32Status)
    {
        var n = CloneEntry(_entry); n.Win32Status = win32Status; return new(n);
    }

    public LogIISEventBuilder SetTimeTaken(int timeTaken)
    {
        var n = CloneEntry(_entry); n.TimeTaken = timeTaken; return new(n);
    }

    public LogIISEventBuilder SetBytesSent(long bytesSent)
    {
        var n = CloneEntry(_entry); n.BytesSent = bytesSent; return new(n);
    }

    public LogIISEventBuilder SetBytesReceived(long bytesReceived)
    {
        var n = CloneEntry(_entry); n.BytesReceived = bytesReceived; return new(n);
    }

    public LogIISEventBuilder SetSiteName(string siteName)
    {
        var n = CloneEntry(_entry); n.SiteName = siteName; return new(n);
    }

    public LogIISEventBuilder SetServerName(string serverName)
    {
        var n = CloneEntry(_entry); n.ServerName = serverName; return new(n);
    }

    public LogIISEventBuilder SetGuid(string guid)
    {
        var n = CloneEntry(_entry); n.Guid = guid; return new(n);
    }

    public async Task Log()
    {
        if (string.IsNullOrEmpty(_entry.Collection))
            _entry.Collection = Collection ?? "default";

        _entry.ApiKey = ApiKey;

        if (string.IsNullOrEmpty(_entry.Guid))
            _entry.Guid = Guid.NewGuid().ToString();

        var logger = GetOrCreateLogger();
        await logger.Log(_entry.ToLog()).ConfigureAwait(false);
    }
}
