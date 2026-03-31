using LogDB.Client.Services;

namespace LogDB.Client.Models;

/// <summary>
/// Represents a Docker container log entry to be sent to LogDB
/// </summary>
public class LogDockerEvent
{
    internal string? ApiKey { get; set; }
    internal bool _IsEncrypted { get; set; }

    public LogDockerEvent() { }

    public LogDockerEvent(string? apiKey, string guid)
    {
        ApiKey = apiKey;
        Guid = guid;
    }

    public string Guid { get; set; } = System.Guid.NewGuid().ToString();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? Collection { get; set; }
    public string? Environment { get; set; }

    public string? ContainerId { get; set; }
    public string? ContainerName { get; set; }
    public string? Image { get; set; }
    public string? Stream { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Level { get; set; } = "Info";
    public string? HostName { get; set; }
    public string? ComposeProject { get; set; }
    public string? ComposeService { get; set; }
    public string? Source { get; set; }
    public Dictionary<string, string> Labels { get; set; } = new();

    /// <summary>
    /// Converts to a Log entry with _sys_type=docker for server-side routing
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
            Application = ContainerName ?? string.Empty,
            Message = Message,
            Level = ParseLevel(Level),
            Source = Source,
        };

        log.AttributesS["_sys_type"] = "docker";
        log.Label.Add("docker");

        if (!string.IsNullOrEmpty(ContainerId))
            log.AttributesS["docker.container.id"] = ContainerId;
        if (!string.IsNullOrEmpty(ContainerName))
        {
            log.AttributesS["docker.container.name"] = ContainerName;
            log.Label.Add(ContainerName);
        }
        if (!string.IsNullOrEmpty(Image))
            log.AttributesS["docker.image"] = Image;
        if (!string.IsNullOrEmpty(Stream))
            log.AttributesS["docker.stream"] = Stream;
        if (!string.IsNullOrEmpty(HostName))
            log.AttributesS["host.name"] = HostName;
        if (!string.IsNullOrEmpty(ComposeProject))
        {
            log.AttributesS["docker.compose.project"] = ComposeProject;
            log.Label.Add(ComposeProject);
        }
        if (!string.IsNullOrEmpty(ComposeService))
            log.AttributesS["docker.compose.service"] = ComposeService;

        foreach (var label in Labels)
            log.AttributesS[$"docker.label.{label.Key}"] = label.Value;

        return log;
    }

    private void EncryptFields()
    {
        if (!string.IsNullOrEmpty(Message) && !Message.StartsWith("encrypted_by_logdb"))
            Message = EncryptionService.Encrypt(Message);
        if (!string.IsNullOrEmpty(HostName) && !HostName.StartsWith("encrypted_by_logdb"))
            HostName = EncryptionService.Encrypt(HostName);
        if (!string.IsNullOrEmpty(Source) && !Source.StartsWith("encrypted_by_logdb"))
            Source = EncryptionService.Encrypt(Source);
    }

    private static LogLevel ParseLevel(string level)
    {
        return level?.ToLowerInvariant() switch
        {
            "trace" => LogLevel.Trace,
            "debug" => LogLevel.Debug,
            "info" or "information" => LogLevel.Info,
            "warning" or "warn" => LogLevel.Warning,
            "error" or "err" => LogLevel.Error,
            "critical" or "fatal" => LogLevel.Critical,
            _ => LogLevel.Info
        };
    }
}
