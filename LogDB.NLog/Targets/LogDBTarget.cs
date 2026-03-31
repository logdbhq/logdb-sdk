using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using NLog.Config;
using NLog.Targets;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using LogDB.Extensions.Logging;
using LogDB.Client.Models;

namespace LogDB.NLog
{
    /// <summary>
    /// NLog target that sends log events to LogDB
    /// </summary>
    [Target("LogDB")]
    public class LogDBTarget : AsyncTaskTarget
    {
        private LogDBTargetOptions? _options;
        private ILogDBClient? _client;
        private LogEventInfoConverter? _converter;
        private bool _disposed;

        /// <summary>
        /// LogDB API key for authentication (required)
        /// </summary>
        public string ApiKey { get; set; } = string.Empty;

        /// <summary>
        /// Service URL for LogDB Writer. If not specified, auto-discovery will be used
        /// </summary>
        public string? ServiceUrl { get; set; }

        /// <summary>
        /// Protocol to use for communication
        /// </summary>
        public LogDBProtocol Protocol { get; set; } = LogDBProtocol.Native;

        /// <summary>
        /// Default collection name for logs
        /// </summary>
        public string DefaultCollection { get; set; } = "logs";

        /// <summary>
        /// Default application name
        /// </summary>
        public string? DefaultApplication { get; set; }

        /// <summary>
        /// Default environment name
        /// </summary>
        public string DefaultEnvironment { get; set; } = "production";

        /// <summary>
        /// Explicit default payload type used when an event does not contain LogDBType.
        /// Keep null to require LogDBType on every event.
        /// </summary>
        public LogDBPayloadType? DefaultPayloadType { get; set; }

        /// <summary>
        /// Minimum log level to send to LogDB
        /// </summary>
        public global::NLog.LogLevel MinimumLevel { get; set; } = global::NLog.LogLevel.Info;

        /// <summary>
        /// Enable batching of log entries
        /// </summary>
        public bool EnableBatching { get; set; } = true;

        /// <summary>
        /// Number of log entries to batch before sending
        /// </summary>
        public new int BatchSize { get; set; } = 100;

        /// <summary>
        /// Maximum time to wait before flushing a batch (in seconds)
        /// </summary>
        public int FlushIntervalSeconds { get; set; } = 5;

        /// <summary>
        /// Enable compression for payloads
        /// </summary>
        public bool EnableCompression { get; set; } = true;

        /// <summary>
        /// Maximum number of retries for failed requests
        /// </summary>
        public int MaxRetries { get; set; } = 3;

        /// <summary>
        /// Enable circuit breaker pattern
        /// </summary>
        public bool EnableCircuitBreaker { get; set; } = true;

        /// <summary>
        /// Enable debug logging for the LogDB client itself
        /// </summary>
        public bool EnableDebugLogging { get; set; } = false;

        /// <summary>
        /// Request timeout in seconds
        /// </summary>
        public int RequestTimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// Fallback target to use if LogDB fails
        /// </summary>
        public Target? FallbackTarget { get; set; }

        /// <summary>
        /// Custom filter function (applied after NLog rules)
        /// </summary>
        public Func<LogEventInfo, bool>? Filter { get; set; }

        /// <summary>
        /// Initialize the target
        /// </summary>
        protected override void InitializeTarget()
        {
            base.InitializeTarget();

            if (string.IsNullOrWhiteSpace(ApiKey))
            {
                throw new NLogConfigurationException("ApiKey is required for LogDB target");
            }

            // Build options from target properties
            _options = new LogDBTargetOptions
            {
                ApiKey = ApiKey,
                ServiceUrl = ServiceUrl,
                Protocol = Protocol,
                DefaultCollection = DefaultCollection,
                DefaultApplication = DefaultApplication,
                DefaultEnvironment = DefaultEnvironment,
                DefaultPayloadType = DefaultPayloadType,
                MinimumLevel = MinimumLevel,
                EnableBatching = EnableBatching,
                BatchSize = BatchSize,
                FlushInterval = TimeSpan.FromSeconds(FlushIntervalSeconds),
                EnableCompression = EnableCompression,
                MaxRetries = MaxRetries,
                EnableCircuitBreaker = EnableCircuitBreaker,
                EnableDebugLogging = EnableDebugLogging,
                RequestTimeout = TimeSpan.FromSeconds(RequestTimeoutSeconds),
                Filter = Filter
            };

            // Create LogDB client
            var logDBOptions = Microsoft.Extensions.Options.Options.Create(_options.ToLogDBLoggerOptions());
            
            // LogDBClient can work without a logger (it's optional)
            // We pass null to avoid circular logging dependencies
            _client = new LogDBClient(logDBOptions, null);

            // Create converter
            _converter = new LogEventInfoConverter(_options);
        }

        /// <summary>
        /// Write log event asynchronously
        /// </summary>
        protected override async Task WriteAsyncTask(LogEventInfo logEvent, CancellationToken cancellationToken)
        {
            if (_disposed || _client == null || _converter == null)
                return;

            // Check minimum level
            if (logEvent.Level < MinimumLevel)
                return;

            // Apply custom filter if configured
            if (Filter != null && !Filter(logEvent))
                return;

            try
            {
                var payloadType = ResolvePayloadType(logEvent);
                switch (payloadType)
                {
                    case LogDBPayloadType.Cache:
                        await _client.LogCacheAsync(ConvertToLogCache(logEvent), cancellationToken).ConfigureAwait(false);
                        break;
                    case LogDBPayloadType.Beat:
                        await _client.LogBeatAsync(ConvertToLogBeat(logEvent), cancellationToken).ConfigureAwait(false);
                        break;
                    default:
                        var log = _converter.Convert(logEvent);
                        await _client.LogAsync(log, cancellationToken).ConfigureAwait(false);
                        break;
                }
            }
            catch (Exception ex)
            {
                HandleError(ex, logEvent);
            }
        }

        /// <summary>
        /// Flush any pending logs
        /// </summary>
        protected override void FlushAsync(global::NLog.Common.AsyncContinuation asyncContinuation)
        {
            if (asyncContinuation == null)
            {
                throw new ArgumentNullException(nameof(asyncContinuation));
            }

            try
            {
                if (_client != null && !_disposed)
                {
                    var flushTask = _client.FlushAsync();
                    if (flushTask.IsCompleted)
                    {
                        CompleteFlush(asyncContinuation, flushTask);
                    }
                    else
                    {
                        flushTask.ContinueWith(
                            t => CompleteFlush(asyncContinuation, t),
                            CancellationToken.None,
                            TaskContinuationOptions.ExecuteSynchronously,
                            TaskScheduler.Default);
                    }
                }
                else
                {
                    asyncContinuation(null);
                }
            }
            catch (Exception ex)
            {
                asyncContinuation(ex);
            }
        }

        private static void CompleteFlush(global::NLog.Common.AsyncContinuation asyncContinuation, Task task)
        {
            if (task.IsFaulted)
            {
                asyncContinuation(task.Exception?.GetBaseException() ?? new InvalidOperationException("LogDB flush failed."));
                return;
            }

            if (task.IsCanceled)
            {
                asyncContinuation(new OperationCanceledException("LogDB flush was canceled."));
                return;
            }

            asyncContinuation(null);
        }

        private LogCache ConvertToLogCache(LogEventInfo logEvent)
        {
            var key = GetPropertyString(logEvent, "LogDBCacheKey");
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new InvalidOperationException("LogDB cache routing requires property LogDBCacheKey.");
            }

            var value = GetPropertyString(logEvent, "LogDBCacheValue") ?? logEvent.FormattedMessage ?? logEvent.Message;
            var collection = GetPropertyString(logEvent, "LogDBCollection") ?? _options?.DefaultCollection;

            var cacheKey = key!;

            var cache = new LogCache
            {
                Key = cacheKey,
                Value = value ?? string.Empty,
                Collection = collection,
                Timestamp = logEvent.TimeStamp.ToUniversalTime()
            };

            if (TryGetIntProperty(logEvent, "LogDBTtlSeconds", out var ttlSeconds))
            {
                cache.TtlSeconds = ttlSeconds;
            }

            return cache;
        }

        private LogBeat ConvertToLogBeat(LogEventInfo logEvent)
        {
            var measurement = GetPropertyString(logEvent, "LogDBMeasurement");
            if (string.IsNullOrWhiteSpace(measurement))
            {
                throw new InvalidOperationException("LogDB beat routing requires property LogDBMeasurement.");
            }

            var beatMeasurement = measurement!;

            var beat = new LogBeat
            {
                Measurement = beatMeasurement,
                Collection = GetPropertyString(logEvent, "LogDBCollection") ?? _options?.DefaultCollection,
                Timestamp = logEvent.TimeStamp.ToUniversalTime()
            };

            beat.Application = GetPropertyString(logEvent, "LogDBApplication")
                ?? _options?.DefaultApplication
                ?? logEvent.LoggerName;
            beat.Environment = GetPropertyString(logEvent, "LogDBEnvironment") ?? _options?.DefaultEnvironment;

            if (TryGetPropertyValue(logEvent, "LogDBTags", out var tagsValue))
            {
                foreach (var item in EnumerateMetaEntries(tagsValue))
                {
                    AddOrUpdateMeta(beat.Tag, item.Key, item.Value);
                }
            }

            if (TryGetPropertyValue(logEvent, "LogDBFields", out var fieldsValue))
            {
                foreach (var item in EnumerateMetaEntries(fieldsValue))
                {
                    AddOrUpdateMeta(beat.Field, item.Key, item.Value);
                }
            }

            foreach (var property in logEvent.Properties)
            {
                var key = property.Key?.ToString();
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                var propertyKey = key!;

                if (propertyKey.StartsWith("Tag.", StringComparison.OrdinalIgnoreCase))
                {
                    AddOrUpdateMeta(beat.Tag, propertyKey.Substring("Tag.".Length), ConvertToString(property.Value));
                    continue;
                }

                if (propertyKey.StartsWith("Field.", StringComparison.OrdinalIgnoreCase))
                {
                    AddOrUpdateMeta(beat.Field, propertyKey.Substring("Field.".Length), ConvertToString(property.Value));
                }
            }

            return beat;
        }

        private LogDBPayloadType ResolvePayloadType(LogEventInfo logEvent)
        {
            if (TryGetPropertyValue(logEvent, "LogDBType", out var rawValue))
            {
                if (TryParsePayloadType(rawValue, out var payloadType))
                {
                    return payloadType;
                }

                throw new InvalidOperationException(
                    $"Invalid LogDBType '{ConvertToString(rawValue)}'. Use " +
                    $"{nameof(LogDBPayloadType)}.{nameof(LogDBPayloadType.Log)}, " +
                    $"{nameof(LogDBPayloadType)}.{nameof(LogDBPayloadType.Cache)}, or " +
                    $"{nameof(LogDBPayloadType)}.{nameof(LogDBPayloadType.Beat)}.");
            }

            if (_options?.DefaultPayloadType is LogDBPayloadType defaultPayloadType)
            {
                return defaultPayloadType;
            }

            throw new InvalidOperationException(
                "LogDBType is required for each NLog event. Set event property LogDBType to a LogDBPayloadType value, " +
                "or configure LogDBTarget.DefaultPayloadType.");
        }

        private static bool TryParsePayloadType(object? value, out LogDBPayloadType payloadType)
        {
            if (value is LogDBPayloadType typedEnum)
            {
                payloadType = typedEnum;
                return true;
            }

            payloadType = default;
            return false;
        }

        private static bool TryGetPropertyValue(LogEventInfo logEvent, string key, out object? value)
        {
            if (logEvent.Properties.TryGetValue(key, out value))
            {
                return true;
            }

            foreach (var property in logEvent.Properties)
            {
                if (string.Equals(property.Key?.ToString(), key, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }

            value = null;
            return false;
        }

        private static string? GetPropertyString(LogEventInfo logEvent, string key)
        {
            return TryGetPropertyValue(logEvent, key, out var value) ? ConvertToString(value) : null;
        }

        private static bool TryGetIntProperty(LogEventInfo logEvent, string key, out int value)
        {
            value = default;
            if (!TryGetPropertyValue(logEvent, key, out var raw) || raw == null)
            {
                return false;
            }

            if (raw is IConvertible convertible)
            {
                try
                {
                    value = convertible.ToInt32(CultureInfo.InvariantCulture);
                    return true;
                }
                catch
                {
                    // fall back to parse
                }
            }

            return int.TryParse(ConvertToString(raw), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        private static IEnumerable<KeyValuePair<string, string>> EnumerateMetaEntries(object? value)
        {
            if (value is IDictionary dictionary)
            {
                foreach (DictionaryEntry entry in dictionary)
                {
                    var key = entry.Key?.ToString();
                    if (!string.IsNullOrWhiteSpace(key))
                    {
                        yield return new KeyValuePair<string, string>(key!, ConvertToString(entry.Value));
                    }
                }
            }
        }

        private static void AddOrUpdateMeta(List<LogMeta> target, string key, string value)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            var existing = target.FirstOrDefault(d => string.Equals(d.Key, key, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                existing.Value = value;
                return;
            }

            target.Add(new LogMeta { Key = key, Value = value });
        }

        private static string ConvertToString(object? value)
        {
            return value switch
            {
                null => string.Empty,
                DateTime dateTime => dateTime.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
                DateTimeOffset dateTimeOffset => dateTimeOffset.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
                _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
            };
        }

        /// <summary>
        /// Close the target
        /// </summary>
        protected override void CloseTarget()
        {
            try
            {
                if (_client != null && !_disposed)
                {
                    // Flush any pending logs synchronously
                    var flushException = (Exception?)null;
                    var flushEvent = new System.Threading.ManualResetEventSlim(false);

                    FlushAsync(ex =>
                    {
                        flushException = ex;
                        flushEvent.Set();
                    });

                    // Wait for flush to complete (with timeout)
                    if (!flushEvent.Wait(TimeSpan.FromSeconds(10)))
                    {
                        global::NLog.Common.InternalLogger.Warn("LogDB target flush timed out during close");
                    }

                    if (flushException != null)
                    {
                        global::NLog.Common.InternalLogger.Error(flushException, "Error flushing LogDB target during close");
                    }

                    flushEvent.Dispose();

                    // Dispose the client
                    if (_client is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                global::NLog.Common.InternalLogger.Error(ex, "Error closing LogDB target");
            }
            finally
            {
                base.CloseTarget();
            }
        }

        /// <summary>
        /// Handle errors that occur during log sending
        /// </summary>
        private void HandleError(Exception ex, LogEventInfo logEvent)
        {
            // Log to internal logger
            global::NLog.Common.InternalLogger.Error(ex, "Failed to send log event to LogDB");

            // Call error callback if configured
            _options?.OnError?.Invoke(ex, logEvent);

            // Write to fallback target if configured
            if (FallbackTarget != null)
            {
                try
                {
                    FallbackTarget.WriteAsyncLogEvent(new global::NLog.Common.AsyncLogEventInfo(logEvent, (ex) => { }));
                }
                catch (Exception fallbackEx)
                {
                    global::NLog.Common.InternalLogger.Error(fallbackEx, "Error writing to fallback target");
                }
            }
        }

        /// <summary>
        /// Dispose the target
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    CloseTarget();
                }
                _disposed = true;
            }
            base.Dispose(disposing);
        }
    }
}






