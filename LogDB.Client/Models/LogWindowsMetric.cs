using LogDB.Client.Services;

namespace LogDB.Client.Models;

/// <summary>
/// Represents a Windows system metric to be sent to LogDB
/// </summary>
public class LogWindowsMetric
{
    internal string? ApiKey { get; set; }
    internal bool _IsEncrypted { get; set; }

    public LogWindowsMetric() { }

    public LogWindowsMetric(string? apiKey, string guid)
    {
        ApiKey = apiKey;
        Guid = guid;
    }

    public string Guid { get; set; } = System.Guid.NewGuid().ToString();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? Collection { get; set; }
    public string? Environment { get; set; }

    /// <summary>
    /// Metric category: "cpu", "memory", "disk", "network"
    /// </summary>
    public string Measurement { get; set; } = string.Empty;

    public string? ServerName { get; set; }

    // CPU metrics
    public double? CpuUsagePercent { get; set; }
    public double? CpuIdlePercent { get; set; }
    public int? CpuCoreCount { get; set; }

    // Memory metrics
    public double? MemoryTotalGb { get; set; }
    public double? MemoryUsedGb { get; set; }
    public double? MemoryFreeGb { get; set; }
    public double? MemoryUsagePercent { get; set; }

    // Disk metrics
    public string? DriveLetter { get; set; }
    public string? DriveType { get; set; }
    public string? FileSystem { get; set; }
    public double? DiskTotalGb { get; set; }
    public double? DiskUsedGb { get; set; }
    public double? DiskFreeGb { get; set; }
    public double? DiskUsagePercent { get; set; }

    // Network metrics
    public string? InterfaceName { get; set; }
    public string? InterfaceType { get; set; }
    public double? NetworkBytesSent { get; set; }
    public double? NetworkBytesReceived { get; set; }
    public double? NetworkSpeedMbps { get; set; }

    /// <summary>
    /// Converts to a Log entry with _sys_type=windows_metric for server-side routing
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
            Application = ServerName ?? string.Empty,
            Message = $"{Measurement} metric from {ServerName ?? "unknown"}",
            Level = LogLevel.Info,
            Source = "windows_metric",
        };

        log.AttributesS["_sys_type"] = "windows_metric";
        log.AttributesS["measurement"] = Measurement;

        if (!string.IsNullOrEmpty(ServerName))
            log.AttributesS["serverName"] = ServerName;
        if (!string.IsNullOrEmpty(Environment))
            log.AttributesS["environment"] = Environment;

        // Tags for disk/network sub-identification
        var tags = new Dictionary<string, string>();
        if (!string.IsNullOrEmpty(DriveLetter)) tags["drive_letter"] = DriveLetter;
        if (!string.IsNullOrEmpty(DriveType)) tags["drive_type"] = DriveType;
        if (!string.IsNullOrEmpty(FileSystem)) tags["file_system"] = FileSystem;
        if (!string.IsNullOrEmpty(InterfaceName)) tags["interface_name"] = InterfaceName;
        if (!string.IsNullOrEmpty(InterfaceType)) tags["interface_type"] = InterfaceType;
        if (tags.Count > 0)
            log.AttributesS["tags"] = System.Text.Json.JsonSerializer.Serialize(tags);

        // Numeric metrics
        if (CpuUsagePercent.HasValue) log.AttributesN["usage_percent"] = CpuUsagePercent.Value;
        if (CpuIdlePercent.HasValue) log.AttributesN["idle_percent"] = CpuIdlePercent.Value;
        if (CpuCoreCount.HasValue) log.AttributesN["core_count"] = CpuCoreCount.Value;
        if (MemoryTotalGb.HasValue) log.AttributesN["total_gb"] = MemoryTotalGb.Value;
        if (MemoryUsedGb.HasValue) log.AttributesN["used_gb"] = MemoryUsedGb.Value;
        if (MemoryFreeGb.HasValue) log.AttributesN["free_gb"] = MemoryFreeGb.Value;
        if (MemoryUsagePercent.HasValue && Measurement == "memory") log.AttributesN["usage_percent"] = MemoryUsagePercent.Value;
        if (DiskTotalGb.HasValue) log.AttributesN["total_gb"] = DiskTotalGb.Value;
        if (DiskUsedGb.HasValue) log.AttributesN["used_gb"] = DiskUsedGb.Value;
        if (DiskFreeGb.HasValue) log.AttributesN["free_gb"] = DiskFreeGb.Value;
        if (DiskUsagePercent.HasValue && Measurement == "disk") log.AttributesN["usage_percent"] = DiskUsagePercent.Value;
        if (NetworkBytesSent.HasValue) log.AttributesN["bytes_sent"] = NetworkBytesSent.Value;
        if (NetworkBytesReceived.HasValue) log.AttributesN["bytes_received"] = NetworkBytesReceived.Value;
        if (NetworkSpeedMbps.HasValue) log.AttributesN["speed_mbps"] = NetworkSpeedMbps.Value;

        return log;
    }

    private void EncryptFields()
    {
        if (!string.IsNullOrEmpty(ServerName) && !ServerName.StartsWith("encrypted_by_logdb"))
            ServerName = EncryptionService.Encrypt(ServerName);
        if (!string.IsNullOrEmpty(InterfaceName) && !InterfaceName.StartsWith("encrypted_by_logdb"))
            InterfaceName = EncryptionService.Encrypt(InterfaceName);
    }
}
