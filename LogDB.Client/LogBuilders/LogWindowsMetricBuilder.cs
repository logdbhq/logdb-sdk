using com.logdb.LogDB;
using LogDB.Client.Models;
using LogDB.Client.Services;

namespace com.logdb.logger.LogBuilders;

public sealed class LogWindowsMetricBuilder
{
    private static LoggerContext _context = new();
    private readonly LogWindowsMetric _entry;
    private static Logger? _logger;

    private LogWindowsMetricBuilder(LogWindowsMetric entry)
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
            throw new InvalidOperationException("LogWindowsMetricBuilder.ApiKey must be set before logging.");

        return _logger ??= Logger.Create(_context.ApiKey);
    }

    public static LogWindowsMetricBuilder Create()
    {
        if (string.IsNullOrWhiteSpace(_context.ApiKey))
            throw new InvalidOperationException("LogWindowsMetricBuilder.ApiKey must be set before Create().");

        return new LogWindowsMetricBuilder(new LogWindowsMetric(_context.ApiKey, Guid.NewGuid().ToString())
        {
            Timestamp = DateTime.UtcNow
        });
    }

    public LogWindowsMetric Build() => _entry;

    private static LogWindowsMetric CloneEntry(LogWindowsMetric o)
    {
        return new LogWindowsMetric(o.ApiKey, o.Guid)
        {
            Collection = o.Collection,
            Environment = o.Environment,
            Timestamp = o.Timestamp,
            Measurement = o.Measurement,
            ServerName = o.ServerName,
            CpuUsagePercent = o.CpuUsagePercent,
            CpuIdlePercent = o.CpuIdlePercent,
            CpuCoreCount = o.CpuCoreCount,
            MemoryTotalGb = o.MemoryTotalGb,
            MemoryUsedGb = o.MemoryUsedGb,
            MemoryFreeGb = o.MemoryFreeGb,
            MemoryUsagePercent = o.MemoryUsagePercent,
            DriveLetter = o.DriveLetter,
            DriveType = o.DriveType,
            FileSystem = o.FileSystem,
            DiskTotalGb = o.DiskTotalGb,
            DiskUsedGb = o.DiskUsedGb,
            DiskFreeGb = o.DiskFreeGb,
            DiskUsagePercent = o.DiskUsagePercent,
            InterfaceName = o.InterfaceName,
            InterfaceType = o.InterfaceType,
            NetworkBytesSent = o.NetworkBytesSent,
            NetworkBytesReceived = o.NetworkBytesReceived,
            NetworkSpeedMbps = o.NetworkSpeedMbps
        };
    }

    public LogWindowsMetricBuilder SetTimestamp(DateTime timestamp)
    {
        var n = CloneEntry(_entry); n.Timestamp = timestamp; return new(n);
    }

    public LogWindowsMetricBuilder SetCollection(string? collection)
    {
        var n = CloneEntry(_entry); n.Collection = collection; return new(n);
    }

    public LogWindowsMetricBuilder SetEnvironment(string environment)
    {
        var n = CloneEntry(_entry); n.Environment = environment; return new(n);
    }

    public LogWindowsMetricBuilder SetMeasurement(string measurement)
    {
        var n = CloneEntry(_entry); n.Measurement = measurement; return new(n);
    }

    public LogWindowsMetricBuilder SetServerName(string serverName, bool isEncrypted = false)
    {
        var n = CloneEntry(_entry); n.ServerName = isEncrypted ? EncryptionService.Encrypt(serverName) : serverName; return new(n);
    }

    public LogWindowsMetricBuilder Encrypt()
    {
        var n = CloneEntry(_entry); n._IsEncrypted = true; return new(n);
    }

    public LogWindowsMetricBuilder SetCpuUsagePercent(double value)
    {
        var n = CloneEntry(_entry); n.CpuUsagePercent = value; return new(n);
    }

    public LogWindowsMetricBuilder SetCpuIdlePercent(double value)
    {
        var n = CloneEntry(_entry); n.CpuIdlePercent = value; return new(n);
    }

    public LogWindowsMetricBuilder SetCpuCoreCount(int value)
    {
        var n = CloneEntry(_entry); n.CpuCoreCount = value; return new(n);
    }

    public LogWindowsMetricBuilder SetMemoryTotalGb(double value)
    {
        var n = CloneEntry(_entry); n.MemoryTotalGb = value; return new(n);
    }

    public LogWindowsMetricBuilder SetMemoryUsedGb(double value)
    {
        var n = CloneEntry(_entry); n.MemoryUsedGb = value; return new(n);
    }

    public LogWindowsMetricBuilder SetMemoryFreeGb(double value)
    {
        var n = CloneEntry(_entry); n.MemoryFreeGb = value; return new(n);
    }

    public LogWindowsMetricBuilder SetMemoryUsagePercent(double value)
    {
        var n = CloneEntry(_entry); n.MemoryUsagePercent = value; return new(n);
    }

    public LogWindowsMetricBuilder SetDriveLetter(string value)
    {
        var n = CloneEntry(_entry); n.DriveLetter = value; return new(n);
    }

    public LogWindowsMetricBuilder SetDriveType(string value)
    {
        var n = CloneEntry(_entry); n.DriveType = value; return new(n);
    }

    public LogWindowsMetricBuilder SetFileSystem(string value)
    {
        var n = CloneEntry(_entry); n.FileSystem = value; return new(n);
    }

    public LogWindowsMetricBuilder SetDiskTotalGb(double value)
    {
        var n = CloneEntry(_entry); n.DiskTotalGb = value; return new(n);
    }

    public LogWindowsMetricBuilder SetDiskUsedGb(double value)
    {
        var n = CloneEntry(_entry); n.DiskUsedGb = value; return new(n);
    }

    public LogWindowsMetricBuilder SetDiskFreeGb(double value)
    {
        var n = CloneEntry(_entry); n.DiskFreeGb = value; return new(n);
    }

    public LogWindowsMetricBuilder SetDiskUsagePercent(double value)
    {
        var n = CloneEntry(_entry); n.DiskUsagePercent = value; return new(n);
    }

    public LogWindowsMetricBuilder SetInterfaceName(string value)
    {
        var n = CloneEntry(_entry); n.InterfaceName = value; return new(n);
    }

    public LogWindowsMetricBuilder SetInterfaceType(string value)
    {
        var n = CloneEntry(_entry); n.InterfaceType = value; return new(n);
    }

    public LogWindowsMetricBuilder SetNetworkBytesSent(double value)
    {
        var n = CloneEntry(_entry); n.NetworkBytesSent = value; return new(n);
    }

    public LogWindowsMetricBuilder SetNetworkBytesReceived(double value)
    {
        var n = CloneEntry(_entry); n.NetworkBytesReceived = value; return new(n);
    }

    public LogWindowsMetricBuilder SetNetworkSpeedMbps(double value)
    {
        var n = CloneEntry(_entry); n.NetworkSpeedMbps = value; return new(n);
    }

    public LogWindowsMetricBuilder SetGuid(string guid)
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
