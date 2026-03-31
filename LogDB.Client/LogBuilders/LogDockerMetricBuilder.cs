using com.logdb.LogDB;
using LogDB.Client.Models;
using LogDB.Client.Services;

namespace com.logdb.logger.LogBuilders;

public sealed class LogDockerMetricBuilder
{
    private static LoggerContext _context = new();
    private readonly LogDockerMetric _entry;
    private static Logger? _logger;

    private LogDockerMetricBuilder(LogDockerMetric entry)
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
            throw new InvalidOperationException("LogDockerMetricBuilder.ApiKey must be set before logging.");

        return _logger ??= Logger.Create(_context.ApiKey);
    }

    public static LogDockerMetricBuilder Create()
    {
        if (string.IsNullOrWhiteSpace(_context.ApiKey))
            throw new InvalidOperationException("LogDockerMetricBuilder.ApiKey must be set before Create().");

        return new LogDockerMetricBuilder(new LogDockerMetric(_context.ApiKey, Guid.NewGuid().ToString())
        {
            Timestamp = DateTime.UtcNow
        });
    }

    public LogDockerMetric Build() => _entry;

    private static LogDockerMetric CloneEntry(LogDockerMetric o)
    {
        return new LogDockerMetric(o.ApiKey, o.Guid)
        {
            Collection = o.Collection,
            Timestamp = o.Timestamp,
            Measurement = o.Measurement,
            ContainerId = o.ContainerId,
            ContainerName = o.ContainerName,
            Image = o.Image,
            ImageTag = o.ImageTag,
            HostName = o.HostName,
            ContainerState = o.ContainerState,
            ContainerStatus = o.ContainerStatus,
            ComposeProject = o.ComposeProject,
            ComposeService = o.ComposeService,
            HealthStatus = o.HealthStatus,
            CpuUsagePercent = o.CpuUsagePercent,
            CpuTotalUsage = o.CpuTotalUsage,
            CpuSystemUsage = o.CpuSystemUsage,
            CpuOnlineCpus = o.CpuOnlineCpus,
            MemoryUsageBytes = o.MemoryUsageBytes,
            MemoryLimitBytes = o.MemoryLimitBytes,
            MemoryUsagePercent = o.MemoryUsagePercent,
            MemoryMaxUsageBytes = o.MemoryMaxUsageBytes,
            NetworkRxBytes = o.NetworkRxBytes,
            NetworkTxBytes = o.NetworkTxBytes,
            NetworkRxPackets = o.NetworkRxPackets,
            NetworkTxPackets = o.NetworkTxPackets,
            BlockIoReadBytes = o.BlockIoReadBytes,
            BlockIoWriteBytes = o.BlockIoWriteBytes,
            PidsCurrent = o.PidsCurrent,
            RestartCount = o.RestartCount,
            Labels = new Dictionary<string, string>(o.Labels)
        };
    }

    public LogDockerMetricBuilder SetTimestamp(DateTime timestamp)
    {
        var n = CloneEntry(_entry); n.Timestamp = timestamp; return new(n);
    }

    public LogDockerMetricBuilder SetCollection(string? collection)
    {
        var n = CloneEntry(_entry); n.Collection = collection; return new(n);
    }

    public LogDockerMetricBuilder SetMeasurement(string measurement)
    {
        var n = CloneEntry(_entry); n.Measurement = measurement; return new(n);
    }

    public LogDockerMetricBuilder SetContainerId(string containerId)
    {
        var n = CloneEntry(_entry); n.ContainerId = containerId; return new(n);
    }

    public LogDockerMetricBuilder SetContainerName(string containerName)
    {
        var n = CloneEntry(_entry); n.ContainerName = containerName; return new(n);
    }

    public LogDockerMetricBuilder SetImage(string image)
    {
        var n = CloneEntry(_entry); n.Image = image; return new(n);
    }

    public LogDockerMetricBuilder SetImageTag(string imageTag)
    {
        var n = CloneEntry(_entry); n.ImageTag = imageTag; return new(n);
    }

    public LogDockerMetricBuilder SetHostName(string hostName, bool isEncrypted = false)
    {
        var n = CloneEntry(_entry); n.HostName = isEncrypted ? EncryptionService.Encrypt(hostName) : hostName; return new(n);
    }

    public LogDockerMetricBuilder Encrypt()
    {
        var n = CloneEntry(_entry); n._IsEncrypted = true; return new(n);
    }

    public LogDockerMetricBuilder SetContainerState(string state)
    {
        var n = CloneEntry(_entry); n.ContainerState = state; return new(n);
    }

    public LogDockerMetricBuilder SetContainerStatus(string status)
    {
        var n = CloneEntry(_entry); n.ContainerStatus = status; return new(n);
    }

    public LogDockerMetricBuilder SetComposeProject(string composeProject)
    {
        var n = CloneEntry(_entry); n.ComposeProject = composeProject; return new(n);
    }

    public LogDockerMetricBuilder SetComposeService(string composeService)
    {
        var n = CloneEntry(_entry); n.ComposeService = composeService; return new(n);
    }

    public LogDockerMetricBuilder SetHealthStatus(string healthStatus)
    {
        var n = CloneEntry(_entry); n.HealthStatus = healthStatus; return new(n);
    }

    public LogDockerMetricBuilder SetCpuUsagePercent(double value)
    {
        var n = CloneEntry(_entry); n.CpuUsagePercent = value; return new(n);
    }

    public LogDockerMetricBuilder SetCpuTotalUsage(long value)
    {
        var n = CloneEntry(_entry); n.CpuTotalUsage = value; return new(n);
    }

    public LogDockerMetricBuilder SetCpuSystemUsage(long value)
    {
        var n = CloneEntry(_entry); n.CpuSystemUsage = value; return new(n);
    }

    public LogDockerMetricBuilder SetCpuOnlineCpus(int value)
    {
        var n = CloneEntry(_entry); n.CpuOnlineCpus = value; return new(n);
    }

    public LogDockerMetricBuilder SetMemoryUsageBytes(long value)
    {
        var n = CloneEntry(_entry); n.MemoryUsageBytes = value; return new(n);
    }

    public LogDockerMetricBuilder SetMemoryLimitBytes(long value)
    {
        var n = CloneEntry(_entry); n.MemoryLimitBytes = value; return new(n);
    }

    public LogDockerMetricBuilder SetMemoryUsagePercent(double value)
    {
        var n = CloneEntry(_entry); n.MemoryUsagePercent = value; return new(n);
    }

    public LogDockerMetricBuilder SetMemoryMaxUsageBytes(long value)
    {
        var n = CloneEntry(_entry); n.MemoryMaxUsageBytes = value; return new(n);
    }

    public LogDockerMetricBuilder SetNetworkRxBytes(long value)
    {
        var n = CloneEntry(_entry); n.NetworkRxBytes = value; return new(n);
    }

    public LogDockerMetricBuilder SetNetworkTxBytes(long value)
    {
        var n = CloneEntry(_entry); n.NetworkTxBytes = value; return new(n);
    }

    public LogDockerMetricBuilder SetNetworkRxPackets(long value)
    {
        var n = CloneEntry(_entry); n.NetworkRxPackets = value; return new(n);
    }

    public LogDockerMetricBuilder SetNetworkTxPackets(long value)
    {
        var n = CloneEntry(_entry); n.NetworkTxPackets = value; return new(n);
    }

    public LogDockerMetricBuilder SetBlockIoReadBytes(long value)
    {
        var n = CloneEntry(_entry); n.BlockIoReadBytes = value; return new(n);
    }

    public LogDockerMetricBuilder SetBlockIoWriteBytes(long value)
    {
        var n = CloneEntry(_entry); n.BlockIoWriteBytes = value; return new(n);
    }

    public LogDockerMetricBuilder SetPidsCurrent(int value)
    {
        var n = CloneEntry(_entry); n.PidsCurrent = value; return new(n);
    }

    public LogDockerMetricBuilder SetRestartCount(int value)
    {
        var n = CloneEntry(_entry); n.RestartCount = value; return new(n);
    }

    public LogDockerMetricBuilder AddLabel(string key, string value)
    {
        var n = CloneEntry(_entry); n.Labels[key] = value; return new(n);
    }

    public LogDockerMetricBuilder SetGuid(string guid)
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
