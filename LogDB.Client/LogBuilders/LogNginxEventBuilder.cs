using com.logdb.LogDB;
using LogDB.Client.Models;
using LogDB.Client.Services;

namespace com.logdb.logger.LogBuilders;

public sealed class LogNginxEventBuilder
{
    private static LoggerContext _context = new();
    private readonly LogNginxEvent _entry;
    private static Logger? _logger;

    private LogNginxEventBuilder(LogNginxEvent entry)
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
            throw new InvalidOperationException("LogNginxEventBuilder.ApiKey must be set before logging.");

        return _logger ??= Logger.Create(_context.ApiKey);
    }

    public static LogNginxEventBuilder Create()
    {
        if (string.IsNullOrWhiteSpace(_context.ApiKey))
            throw new InvalidOperationException("LogNginxEventBuilder.ApiKey must be set before Create().");

        return new LogNginxEventBuilder(new LogNginxEvent(_context.ApiKey, Guid.NewGuid().ToString())
        {
            Timestamp = DateTime.UtcNow
        });
    }

    public LogNginxEvent Build() => _entry;

    private static LogNginxEvent CloneEntry(LogNginxEvent o)
    {
        return new LogNginxEvent(o.ApiKey, o.Guid)
        {
            Collection = o.Collection,
            Timestamp = o.Timestamp,
            LogType = o.LogType,
            TargetName = o.TargetName,
            HostName = o.HostName,
            SourceFile = o.SourceFile,
            Message = o.Message,
            Level = o.Level,
            RemoteAddress = o.RemoteAddress,
            Method = o.Method,
            Path = o.Path,
            Protocol = o.Protocol,
            StatusCode = o.StatusCode,
            ResponseBytes = o.ResponseBytes,
            Referer = o.Referer,
            UserAgent = o.UserAgent,
            RequestTime = o.RequestTime,
            ServerName = o.ServerName,
            Severity = o.Severity,
            Pid = o.Pid,
            Tid = o.Tid,
            ConnectionId = o.ConnectionId,
            Upstream = o.Upstream
        };
    }

    public LogNginxEventBuilder SetTimestamp(DateTime timestamp)
    {
        var n = CloneEntry(_entry); n.Timestamp = timestamp; return new(n);
    }

    public LogNginxEventBuilder SetCollection(string? collection)
    {
        var n = CloneEntry(_entry); n.Collection = collection; return new(n);
    }

    public LogNginxEventBuilder SetLogType(string logType)
    {
        var n = CloneEntry(_entry); n.LogType = logType; return new(n);
    }

    public LogNginxEventBuilder SetTargetName(string targetName)
    {
        var n = CloneEntry(_entry); n.TargetName = targetName; return new(n);
    }

    public LogNginxEventBuilder SetHostName(string hostName)
    {
        var n = CloneEntry(_entry); n.HostName = hostName; return new(n);
    }

    public LogNginxEventBuilder SetSourceFile(string sourceFile)
    {
        var n = CloneEntry(_entry); n.SourceFile = sourceFile; return new(n);
    }

    public LogNginxEventBuilder SetMessage(string message, bool isEncrypted = false)
    {
        var n = CloneEntry(_entry); n.Message = isEncrypted ? EncryptionService.Encrypt(message) : message; return new(n);
    }

    public LogNginxEventBuilder SetLevel(string level)
    {
        var n = CloneEntry(_entry); n.Level = level; return new(n);
    }

    public LogNginxEventBuilder SetRemoteAddress(string remoteAddress, bool isEncrypted = false)
    {
        var n = CloneEntry(_entry); n.RemoteAddress = isEncrypted ? EncryptionService.Encrypt(remoteAddress) : remoteAddress; return new(n);
    }

    public LogNginxEventBuilder SetMethod(string method)
    {
        var n = CloneEntry(_entry); n.Method = method; return new(n);
    }

    public LogNginxEventBuilder SetPath(string path)
    {
        var n = CloneEntry(_entry); n.Path = path; return new(n);
    }

    public LogNginxEventBuilder SetProtocol(string protocol)
    {
        var n = CloneEntry(_entry); n.Protocol = protocol; return new(n);
    }

    public LogNginxEventBuilder SetStatusCode(int statusCode)
    {
        var n = CloneEntry(_entry); n.StatusCode = statusCode; return new(n);
    }

    public LogNginxEventBuilder SetResponseBytes(long responseBytes)
    {
        var n = CloneEntry(_entry); n.ResponseBytes = responseBytes; return new(n);
    }

    public LogNginxEventBuilder SetReferer(string referer, bool isEncrypted = false)
    {
        var n = CloneEntry(_entry); n.Referer = isEncrypted ? EncryptionService.Encrypt(referer) : referer; return new(n);
    }

    public LogNginxEventBuilder SetUserAgent(string userAgent, bool isEncrypted = false)
    {
        var n = CloneEntry(_entry); n.UserAgent = isEncrypted ? EncryptionService.Encrypt(userAgent) : userAgent; return new(n);
    }

    public LogNginxEventBuilder Encrypt()
    {
        var n = CloneEntry(_entry); n._IsEncrypted = true; return new(n);
    }

    public LogNginxEventBuilder SetRequestTime(double requestTime)
    {
        var n = CloneEntry(_entry); n.RequestTime = requestTime; return new(n);
    }

    public LogNginxEventBuilder SetServerName(string serverName)
    {
        var n = CloneEntry(_entry); n.ServerName = serverName; return new(n);
    }

    public LogNginxEventBuilder SetSeverity(string severity)
    {
        var n = CloneEntry(_entry); n.Severity = severity; return new(n);
    }

    public LogNginxEventBuilder SetPid(int pid)
    {
        var n = CloneEntry(_entry); n.Pid = pid; return new(n);
    }

    public LogNginxEventBuilder SetTid(int tid)
    {
        var n = CloneEntry(_entry); n.Tid = tid; return new(n);
    }

    public LogNginxEventBuilder SetConnectionId(long connectionId)
    {
        var n = CloneEntry(_entry); n.ConnectionId = connectionId; return new(n);
    }

    public LogNginxEventBuilder SetUpstream(string upstream)
    {
        var n = CloneEntry(_entry); n.Upstream = upstream; return new(n);
    }

    public LogNginxEventBuilder SetGuid(string guid)
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
