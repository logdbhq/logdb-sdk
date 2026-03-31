using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using com.logdb.LogDB;
using LogDB.Client.Models;
using LogDB.Extensions.Logging;

namespace com.logdb.logger;

/// <summary>
/// Thread-safe, high-performance LogDB client.
/// This acts as a facade over the modern LogDB.Extensions.Logging.LogDBClient.
/// </summary>
public class Logger : ILogger, IAsyncDisposable, IDisposable
{
    private readonly LogDBClient _client;
    private readonly LogDBLoggerOptions _logDbOptions;
    private bool _disposed;

    #region Public Properties

    public string ApiKey => _logDbOptions.ApiKey;

    [Obsolete("Use ApiKey property instead")]
    public LoggerContext? GetContext() => new LoggerContext { ApiKey = ApiKey };

    #endregion

    #region Constructors and Factory

    private Logger(string apiKey, LoggerOptions options)
    {
        if (string.IsNullOrWhiteSpace(apiKey)) throw new ArgumentNullException(nameof(apiKey));
        if (options == null) throw new ArgumentNullException(nameof(options));

        _logDbOptions = new LogDBLoggerOptions
        {
            ApiKey = apiKey,
            ServiceUrl = options.ServiceUrl,
            // Map settings
            FlushInterval = TimeSpan.FromSeconds(5),
            BatchSize = 100,
            EnableBatching = true,
            EnableCompression = true,
            // Retry settings defaults from LogDBClient are usually good, but we can override if LoggerOptions had them
            // LoggerOptions.ConnectionTimeout is not directly mapped but handled by policies
        };

        if (options.OnError != null)
        {
            _logDbOptions.OnError = (ex, logs) => 
            {
                options.OnError(ex, "Error processing batch");
            };
        }

        // Create the client with options wrapper
        var optionsWrapper = Options.Create(_logDbOptions);
        _client = new LogDBClient(optionsWrapper, null); // No internal logger to avoid Console noise
    }

    /// <summary>
    /// Creates a new Logger instance.
    /// </summary>
    public static Task<Logger> CreateAsync(string apiKey) => CreateAsync(apiKey, null);

    /// <summary>
    /// Creates a new Logger instance with optional configuration.
    /// </summary>
    public static Task<Logger> CreateAsync(string apiKey, Action<LoggerOptions>? configure = null)
    {
        var options = new LoggerOptions();
        configure?.Invoke(options);

        var logger = new Logger(apiKey, options);
        // Initialization is now implicit in LogDBClient
        return Task.FromResult(logger);
    }

    internal static Logger Create(string apiKey) => Create(apiKey, null);

    internal static Logger Create(string apiKey, Action<LoggerOptions>? configure = null)
    {
        var options = new LoggerOptions();
        configure?.Invoke(options);
        return new Logger(apiKey, options);
    }

    [Obsolete("Use Logger.CreateAsync() instead for better async performance")]
    public Logger(string apiKey) : this(apiKey, new LoggerOptions())
    {
    }

    #endregion

    #region Public Log Methods

    public com.logdb.LogDB.LogBuilders.LogEventBuilder Event() => com.logdb.LogDB.LogBuilders.LogEventBuilder.Create(this);
    [Obsolete("LogPoint is coming soon and is currently disabled in the public SDK.")]
    public com.logdb.logger.LogBuilders.LogPointBuilder Point()
    {
        throw new NotSupportedException("Logger.Point() is marked [Soon] and is not available in this public SDK build yet.");
    }

    [Obsolete("LogRelation is coming soon and is currently disabled in the public SDK.")]
    public com.logdb.LogDB.LogBuilders.LogRelationBuilder Relation()
    {
        throw new NotSupportedException("Logger.Relation() is marked [Soon] and is not available in this public SDK build yet.");
    }

    public async Task<LogResponseStatus> Log(Log logEntry)
    {
        return await _client.LogAsync(logEntry);
    }

    [Obsolete("LogPoint is coming soon and is currently disabled in the public SDK.")]
    public Task<LogResponseStatus> Log(LogPoint logEntry)
    {
        return Task.FromException<LogResponseStatus>(
            new NotSupportedException("Logger.Log(LogPoint) is marked [Soon] and is not available in this public SDK build yet."));
    }

    public async Task<LogResponseStatus> Log(LogBeat logEntry)
    {
        return await _client.LogBeatAsync(logEntry);
    }

    public async Task<LogResponseStatus> Log(LogCache logEntry)
    {
        return await _client.LogCacheAsync(logEntry);
    }

    [Obsolete("LogRelation is coming soon and is currently disabled in the public SDK.")]
    public Task<LogResponseStatus> Log(LogRelation logEntry)
    {
        return Task.FromException<LogResponseStatus>(
            new NotSupportedException("Logger.Log(LogRelation) is marked [Soon] and is not available in this public SDK build yet."));
    }

    #endregion

    #region Public Batch Log Methods

    public async Task<LogResponseStatus> Log(IEnumerable<Log> logEntries)
    {
        if (logEntries == null || !logEntries.Any()) return LogResponseStatus.Success;
        return await _client.SendLogBatchAsync(logEntries.ToList());
    }

    [Obsolete("LogPoint batch writes are coming soon and are currently disabled in the public SDK.")]
    public Task<LogResponseStatus> Log(IEnumerable<LogPoint> logEntries)
    {
        return Task.FromException<LogResponseStatus>(
            new NotSupportedException("Logger.Log(IEnumerable<LogPoint>) is marked [Soon] and is not available in this public SDK build yet."));
    }

    public async Task<LogResponseStatus> Log(IEnumerable<LogBeat> logEntries)
    {
        if (logEntries == null || !logEntries.Any()) return LogResponseStatus.Success;
        return await _client.SendLogBeatBatchAsync(logEntries.ToList());
    }

    public async Task<LogResponseStatus> Log(IEnumerable<LogCache> logEntries)
    {
        if (logEntries == null || !logEntries.Any()) return LogResponseStatus.Success;
        return await _client.SendLogCacheBatchAsync(logEntries.ToList());
    }

    [Obsolete("LogRelation batch writes are coming soon and are currently disabled in the public SDK.")]
    public Task<LogResponseStatus> Log(IEnumerable<LogRelation> logEntries)
    {
        return Task.FromException<LogResponseStatus>(
            new NotSupportedException("Logger.Log(IEnumerable<LogRelation>) is marked [Soon] and is not available in this public SDK build yet."));
    }

    #endregion

    #region Disposal

    public void Dispose()
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        
        await _client.DisposeAsync();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    #endregion
}

public class LoggerOptions
{
    public string? ServiceUrl { get; set; }
    public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(10);
    public int CompressionThresholdBytes { get; set; } = 1024;
    public Action<Exception, string>? OnError { get; set; }
}
