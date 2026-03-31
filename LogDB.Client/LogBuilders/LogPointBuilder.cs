using System;
using System.Globalization;
using com.logdb.LogDB;
using LogDB.Client.Models;

namespace com.logdb.logger.LogBuilders;

[Obsolete("LogPoint is coming soon and is currently disabled in the public SDK.")]
public sealed class LogPointBuilder
{
    private readonly ILogger _logger;
    private readonly LogPoint _entry;

    public List<LogMeta> Tag { get; set; }
    public List<LogMeta> Field { get; set; }
    
    internal LogPointBuilder(ILogger logger, LogPoint entry)
    {
        _logger = logger;
        _entry = entry;
        Tag = entry.Tag;
        Field = entry.Field;
    }

    public static LogPointBuilder Create(ILogger logger)
    {
        throw new NotSupportedException("LogPointBuilder is marked [Soon] and is not available in this public SDK build yet.");
    }

    public LogPointBuilder SetMeasurement(string measurement)
    {
        var newEntry = CloneEntry(_entry);
        newEntry.Measurement = measurement;
        return new LogPointBuilder(_logger, newEntry);
    }
    
    public LogPointBuilder AddTag(string key, string value)
    {
        var newEntry = CloneEntry(_entry);
        newEntry.Tag.Add(new LogMeta { Key = key, Value = value });
        return new LogPointBuilder(_logger, newEntry);
    }

    public LogPointBuilder AddField(string key, string value)
    {
        var newEntry = CloneEntry(_entry);
        newEntry.Field.Add(new LogMeta { Key = key, Value = value });
        return new LogPointBuilder(_logger, newEntry);
    }

    public LogPointBuilder AddField(string key, double value)
    {
        return AddField(key, value.ToString(CultureInfo.InvariantCulture));
    }

    public LogPointBuilder AddField(string key, int value)
    {
        return AddField(key, value.ToString());
    }

    public LogPointBuilder AddField(string key, bool value)
    {
        return AddField(key, value.ToString().ToLowerInvariant());
    }

    public LogPointBuilder SetTimestamp(DateTime timestamp)
    {
        var newEntry = CloneEntry(_entry);
        newEntry.Timestamp = timestamp;
        return new LogPointBuilder(_logger, newEntry);
    }

    public LogPoint Build()
    {
        if (String.IsNullOrEmpty(_entry.Collection))
            _entry.Collection = "default";
        
        return _entry;
    }

    private static LogPoint CloneEntry(LogPoint original)
    {
        return new LogPoint()
        {
            Collection = original.Collection,
            Measurement = original.Measurement,
            Timestamp = original.Timestamp,
            Tag = [.. original.Tag],
            Field = [..original.Field],
            ApiKey = original.ApiKey,
            Guid = original.Guid,
        };
    }

    public LogPointBuilder SetCollection(string collection)
    {
        var newEntry = CloneEntry(_entry);
        newEntry.Collection = collection;
        return new LogPointBuilder(_logger, newEntry);
    }
    
    public LogPointBuilder SetGuid(string guid)
    {
        var newEntry = CloneEntry(_entry);
        newEntry.Guid = guid;
        return new LogPointBuilder(_logger, newEntry);
    }

    public async Task Log()
    {
        await Task.FromException(new NotSupportedException(
            "LogPointBuilder.Log() is marked [Soon] and is not available in this public SDK build yet."));
    }

    public LogPointBuilder SetStatus(LogLevel status)
    {
        var newEntry = CloneEntry(_entry);
        newEntry.Field.Add(new LogMeta(){ Key = "level", Value = status.ToString()});
        return new LogPointBuilder(_logger, newEntry);
    }
}
