using com.logdb.LogDB;
using LogDB.Client.Models;
using LogDB.Client.Services;

namespace com.logdb.logger.LogBuilders;

public sealed class LogDockerEventBuilder
{
    private static LoggerContext _context = new();
    private readonly LogDockerEvent _entry;
    private static Logger? _logger;

    private LogDockerEventBuilder(LogDockerEvent entry)
    {
        _entry = entry;
    }

    public static string? Environment { get; set; }
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
            throw new InvalidOperationException("LogDockerEventBuilder.ApiKey must be set before logging.");

        return _logger ??= Logger.Create(_context.ApiKey);
    }

    public static LogDockerEventBuilder Create()
    {
        if (string.IsNullOrWhiteSpace(_context.ApiKey))
            throw new InvalidOperationException("LogDockerEventBuilder.ApiKey must be set before Create().");

        return new LogDockerEventBuilder(new LogDockerEvent(_context.ApiKey, Guid.NewGuid().ToString())
        {
            Timestamp = DateTime.UtcNow
        });
    }

    public LogDockerEvent Build() => _entry;

    private static LogDockerEvent CloneEntry(LogDockerEvent o)
    {
        return new LogDockerEvent(o.ApiKey, o.Guid)
        {
            Collection = o.Collection,
            Environment = o.Environment,
            Timestamp = o.Timestamp,
            ContainerId = o.ContainerId,
            ContainerName = o.ContainerName,
            Image = o.Image,
            Stream = o.Stream,
            Message = o.Message,
            Level = o.Level,
            HostName = o.HostName,
            ComposeProject = o.ComposeProject,
            ComposeService = o.ComposeService,
            Source = o.Source,
            Labels = new Dictionary<string, string>(o.Labels)
        };
    }

    public LogDockerEventBuilder SetTimestamp(DateTime timestamp)
    {
        var n = CloneEntry(_entry); n.Timestamp = timestamp; return new(n);
    }

    public LogDockerEventBuilder SetCollection(string? collection)
    {
        var n = CloneEntry(_entry); n.Collection = collection; return new(n);
    }

    public LogDockerEventBuilder SetEnvironment(string environment)
    {
        var n = CloneEntry(_entry); n.Environment = environment; return new(n);
    }

    public LogDockerEventBuilder SetContainerId(string containerId)
    {
        var n = CloneEntry(_entry); n.ContainerId = containerId; return new(n);
    }

    public LogDockerEventBuilder SetContainerName(string containerName)
    {
        var n = CloneEntry(_entry); n.ContainerName = containerName; return new(n);
    }

    public LogDockerEventBuilder SetImage(string image)
    {
        var n = CloneEntry(_entry); n.Image = image; return new(n);
    }

    public LogDockerEventBuilder SetStream(string stream)
    {
        var n = CloneEntry(_entry); n.Stream = stream; return new(n);
    }

    public LogDockerEventBuilder SetMessage(string message, bool isEncrypted = false)
    {
        var n = CloneEntry(_entry); n.Message = isEncrypted ? EncryptionService.Encrypt(message) : message; return new(n);
    }

    public LogDockerEventBuilder SetLevel(string level)
    {
        var n = CloneEntry(_entry); n.Level = level; return new(n);
    }

    public LogDockerEventBuilder SetHostName(string hostName, bool isEncrypted = false)
    {
        var n = CloneEntry(_entry); n.HostName = isEncrypted ? EncryptionService.Encrypt(hostName) : hostName; return new(n);
    }

    public LogDockerEventBuilder SetComposeProject(string composeProject)
    {
        var n = CloneEntry(_entry); n.ComposeProject = composeProject; return new(n);
    }

    public LogDockerEventBuilder SetComposeService(string composeService)
    {
        var n = CloneEntry(_entry); n.ComposeService = composeService; return new(n);
    }

    public LogDockerEventBuilder SetSource(string source, bool isEncrypted = false)
    {
        var n = CloneEntry(_entry); n.Source = isEncrypted ? EncryptionService.Encrypt(source) : source; return new(n);
    }

    public LogDockerEventBuilder Encrypt()
    {
        var n = CloneEntry(_entry); n._IsEncrypted = true; return new(n);
    }

    public LogDockerEventBuilder AddLabel(string key, string value)
    {
        var n = CloneEntry(_entry); n.Labels[key] = value; return new(n);
    }

    public LogDockerEventBuilder SetGuid(string guid)
    {
        var n = CloneEntry(_entry); n.Guid = guid; return new(n);
    }

    public async Task Log()
    {
        if (string.IsNullOrEmpty(_entry.Environment) && !string.IsNullOrEmpty(Environment))
            _entry.Environment = Environment;
        if (string.IsNullOrEmpty(_entry.Collection))
            _entry.Collection = Collection ?? "default";

        _entry.ApiKey = ApiKey;

        if (string.IsNullOrEmpty(_entry.Guid))
            _entry.Guid = Guid.NewGuid().ToString();

        var logger = GetOrCreateLogger();
        await logger.Log(_entry.ToLog()).ConfigureAwait(false);
    }
}
