using LogDB.Client.Services;

namespace LogDB.Client.Models;

/// <summary>
/// Represents Docker container metrics to be sent to LogDB
/// </summary>
public class LogDockerMetric
{
    internal string? ApiKey { get; set; }
    internal bool _IsEncrypted { get; set; }

    public LogDockerMetric() { }

    public LogDockerMetric(string? apiKey, string guid)
    {
        ApiKey = apiKey;
        Guid = guid;
    }

    public string Guid { get; set; } = System.Guid.NewGuid().ToString();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? Collection { get; set; }
    public string Measurement { get; set; } = "container_stats";

    // Container identification
    public string? ContainerId { get; set; }
    public string? ContainerName { get; set; }
    public string? Image { get; set; }
    public string? ImageTag { get; set; }
    public string? HostName { get; set; }
    public string? ContainerState { get; set; }
    public string? ContainerStatus { get; set; }
    public string? ComposeProject { get; set; }
    public string? ComposeService { get; set; }
    public string? HealthStatus { get; set; }

    // CPU metrics
    public double? CpuUsagePercent { get; set; }
    public long? CpuTotalUsage { get; set; }
    public long? CpuSystemUsage { get; set; }
    public int? CpuOnlineCpus { get; set; }

    // Memory metrics
    public long? MemoryUsageBytes { get; set; }
    public long? MemoryLimitBytes { get; set; }
    public double? MemoryUsagePercent { get; set; }
    public long? MemoryMaxUsageBytes { get; set; }

    // Network metrics
    public long? NetworkRxBytes { get; set; }
    public long? NetworkTxBytes { get; set; }
    public long? NetworkRxPackets { get; set; }
    public long? NetworkTxPackets { get; set; }

    // Block I/O metrics
    public long? BlockIoReadBytes { get; set; }
    public long? BlockIoWriteBytes { get; set; }

    // Process metrics
    public int? PidsCurrent { get; set; }
    public int? RestartCount { get; set; }

    public Dictionary<string, string> Labels { get; set; } = new();

    /// <summary>
    /// Converts to a Log entry with _sys_type=docker_metric for server-side routing
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
            Application = ContainerName ?? string.Empty,
            Message = $"Container metrics for {ContainerName ?? ContainerId ?? "unknown"}",
            Level = LogLevel.Info,
            Source = "docker_metric",
        };

        log.AttributesS["_sys_type"] = "docker_metric";
        log.AttributesS["measurement"] = Measurement;
        log.Label.Add("docker-metrics");

        if (!string.IsNullOrEmpty(ContainerId))
            log.AttributesS["docker.container.id"] = ContainerId;
        if (!string.IsNullOrEmpty(ContainerName))
        {
            log.AttributesS["docker.container.name"] = ContainerName;
            log.Label.Add(ContainerName);
        }
        if (!string.IsNullOrEmpty(Image))
            log.AttributesS["docker.image"] = Image;
        if (!string.IsNullOrEmpty(ImageTag))
            log.AttributesS["docker.image.tag"] = ImageTag;
        if (!string.IsNullOrEmpty(HostName))
            log.AttributesS["host.name"] = HostName;
        if (!string.IsNullOrEmpty(ContainerState))
            log.AttributesS["docker.container.state"] = ContainerState;
        if (!string.IsNullOrEmpty(ContainerStatus))
            log.AttributesS["docker.container.status"] = ContainerStatus;
        if (!string.IsNullOrEmpty(ComposeProject))
        {
            log.AttributesS["docker.compose.project"] = ComposeProject;
            log.Label.Add(ComposeProject);
        }
        if (!string.IsNullOrEmpty(ComposeService))
            log.AttributesS["docker.compose.service"] = ComposeService;
        if (!string.IsNullOrEmpty(HealthStatus))
            log.AttributesS["docker.health.status"] = HealthStatus;

        foreach (var label in Labels)
            log.AttributesS[$"docker.label.{label.Key}"] = label.Value;

        // Numeric metrics
        if (CpuUsagePercent.HasValue) log.AttributesN["cpu_usage_percent"] = CpuUsagePercent.Value;
        if (CpuTotalUsage.HasValue) log.AttributesN["cpu_total_usage"] = CpuTotalUsage.Value;
        if (CpuSystemUsage.HasValue) log.AttributesN["cpu_system_usage"] = CpuSystemUsage.Value;
        if (CpuOnlineCpus.HasValue) log.AttributesN["cpu_online_cpus"] = CpuOnlineCpus.Value;
        if (MemoryUsageBytes.HasValue) log.AttributesN["memory_usage_bytes"] = MemoryUsageBytes.Value;
        if (MemoryLimitBytes.HasValue) log.AttributesN["memory_limit_bytes"] = MemoryLimitBytes.Value;
        if (MemoryUsagePercent.HasValue) log.AttributesN["memory_usage_percent"] = MemoryUsagePercent.Value;
        if (MemoryMaxUsageBytes.HasValue) log.AttributesN["memory_max_usage_bytes"] = MemoryMaxUsageBytes.Value;
        if (NetworkRxBytes.HasValue) log.AttributesN["network_rx_bytes"] = NetworkRxBytes.Value;
        if (NetworkTxBytes.HasValue) log.AttributesN["network_tx_bytes"] = NetworkTxBytes.Value;
        if (NetworkRxPackets.HasValue) log.AttributesN["network_rx_packets"] = NetworkRxPackets.Value;
        if (NetworkTxPackets.HasValue) log.AttributesN["network_tx_packets"] = NetworkTxPackets.Value;
        if (BlockIoReadBytes.HasValue) log.AttributesN["block_io_read_bytes"] = BlockIoReadBytes.Value;
        if (BlockIoWriteBytes.HasValue) log.AttributesN["block_io_write_bytes"] = BlockIoWriteBytes.Value;
        if (PidsCurrent.HasValue) log.AttributesN["pids_current"] = PidsCurrent.Value;
        if (RestartCount.HasValue) log.AttributesN["restart_count"] = RestartCount.Value;

        return log;
    }

    private void EncryptFields()
    {
        if (!string.IsNullOrEmpty(HostName) && !HostName.StartsWith("encrypted_by_logdb"))
            HostName = EncryptionService.Encrypt(HostName);
    }
}
