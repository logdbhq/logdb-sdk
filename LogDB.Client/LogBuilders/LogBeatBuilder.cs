using System.Globalization;
using com.logdb.LogDB;
using LogDB.Client.Models;

namespace com.logdb.logger.LogBuilders;

public sealed class LogBeatBuilder
{
    private static LoggerContext _context = new();
    private readonly LogBeat _entry;
    private static Logger? _logger;

    private LogBeatBuilder(LogBeat entry)
    {
        _entry = entry;
    }

    public static string? Environment { get; set; }

    public static string? Application { get; set; }

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

    public static string? Collection { get; set; }

    private static Logger GetOrCreateLogger()
    {
        if (string.IsNullOrWhiteSpace(_context.ApiKey))
            throw new InvalidOperationException("LogBeatBuilder.ApiKey must be set before logging.");

        return _logger ??= Logger.Create(_context.ApiKey);
    }

    public static LogBeatBuilder Create()
    {
        if (string.IsNullOrWhiteSpace(_context.ApiKey))
            throw new InvalidOperationException("LogBeatBuilder.ApiKey must be set before Create().");

        return new LogBeatBuilder(new LogBeat(_context.ApiKey, Guid.NewGuid().ToString())
        {
            Measurement = string.Empty,
            Environment = string.Empty,
            Tag = [],
            Field = [],
            Timestamp = DateTime.UtcNow
        });
    }

    public static void SetApiKey(string apiKey)
    {
        ApiKey = apiKey;
    }

    public LogBeatBuilder SetTimestamp(DateTime timestamp)
    {
        var newEntry = CloneEntry(_entry);
        newEntry.Timestamp = timestamp;
        return new LogBeatBuilder(newEntry);
    }

    public LogBeat Build()
    {
        return _entry;
    }

    private static LogBeat CloneEntry(LogBeat original)
    {
        return new LogBeat(original.ApiKey, original.Guid)
        {
            Collection = original.Collection,
            Measurement = original.Measurement,
            Environment = original.Environment,
            Timestamp = original.Timestamp,
            Tag = [.. original.Tag],
            Field = [..original.Field]
        };
    }

    public LogBeatBuilder SetCollection(string? collection)
    {
        var newEntry = CloneEntry(_entry);
        newEntry.Collection = collection;
        return new LogBeatBuilder(newEntry);
    }

    public LogBeatBuilder SetApplication(string application)
    {
        var newEntry = CloneEntry(_entry);
        newEntry.Tag.Add(new LogMeta() { Key = "application", Value = application });
        return new LogBeatBuilder(newEntry);
    }

    public LogBeatBuilder SetEnvironment(string environment)
    {
        var newEntry = CloneEntry(_entry);
        newEntry.Environment = environment;
        return new LogBeatBuilder(newEntry);
    }

    public LogBeatBuilder SetLogLevel(LogLevel status)
    {
        var newEntry = CloneEntry(_entry);
        newEntry.Field.Add(new LogMeta() { Key = "level", Value = status.ToString() });
        return new LogBeatBuilder(newEntry);
    }

    public LogBeatBuilder SetMeasurement(string measurement)
    {
        var newEntry = CloneEntry(_entry);
        newEntry.Measurement = measurement;
        return new LogBeatBuilder(newEntry);
    }

    
    public LogBeatBuilder SetGuid(string guid)
    {
        var newEntry = CloneEntry(_entry);
        newEntry.Guid = guid;
        return new LogBeatBuilder(newEntry);
    }

    public async Task Log()
    {
        if (String.IsNullOrEmpty(_entry.Environment) && !String.IsNullOrEmpty(Environment))
            _entry.Environment = Environment;
        if (String.IsNullOrEmpty(_entry.Application) && !String.IsNullOrEmpty(Application))
            _entry.Application = Application;

        // Only set default collection if it's completely empty, don't override user-set values
        if (String.IsNullOrEmpty(_entry.Collection))
            _entry.Collection = "default";
        
        _entry.ApiKey = ApiKey;
        
        // Only generate a new GUID if one is not already set (allows for idempotency on retries)
        if (string.IsNullOrEmpty(_entry.Guid))
        {
            _entry.Guid = Guid.NewGuid().ToString();
        }

        var logger = GetOrCreateLogger();
        await logger.Log(_entry).ConfigureAwait(false);
    }

    
}
