using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Display;
using LogDB.Client.Models;
using LogDB.Extensions.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using LogDBLog = LogDB.Client.Models.Log;
using LogDBLogLevel = LogDB.Client.Models.LogLevel;

namespace LogDB.Serilog
{
    /// <summary>
    /// Serilog sink that sends log events to LogDB
    /// </summary>
    public class LogDBSink : ILogEventSink, IDisposable
    {
        private readonly LogDBSinkOptions _options;
        private readonly ILogDBClient _client;
        private readonly MessageTemplateTextFormatter? _formatter;
        private readonly ILogger<LogDBSink>? _logger;
        private bool _disposed;

        /// <summary>
        /// Creates a new LogDB sink
        /// </summary>
        public LogDBSink(LogDBSinkOptions options, ILoggerFactory? loggerFactory = null)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            
            if (string.IsNullOrWhiteSpace(options.ApiKey))
            {
                throw new ArgumentException("ApiKey is required", nameof(options));
            }

            _logger = loggerFactory?.CreateLogger<LogDBSink>();

            // Create LogDB client
            var logDBOptions = Microsoft.Extensions.Options.Options.Create(options.ToLogDBLoggerOptions());
            var clientLogger = loggerFactory?.CreateLogger<LogDBClient>();
            _client = new LogDBClient(logDBOptions, clientLogger);

            // Create formatter if format provider is specified
            if (options.FormatProvider != null)
            {
                _formatter = new MessageTemplateTextFormatter("{Message}", options.FormatProvider);
            }
        }

        /// <summary>
        /// Emit a log event to LogDB
        /// </summary>
        public void Emit(LogEvent logEvent)
        {
            if (_disposed)
                return;

            // Check minimum level
            if (logEvent.Level < _options.RestrictedToMinimumLevel)
                return;

            // Apply custom filter if provided
            if (_options.Filter != null && !_options.Filter(logEvent))
                return;

            try
            {
                var sendTask = SendAsync(logEvent);
#if NETFRAMEWORK
                if (sendTask.Status != TaskStatus.RanToCompletion)
#else
                if (!sendTask.IsCompletedSuccessfully)
#endif
                {
                    sendTask.GetAwaiter().GetResult();
                }
            }
            catch (Exception ex)
            {
                _options.OnError?.Invoke(ex, logEvent);
                _logger?.LogError(ex, "Error sending log event to LogDB");
            }
        }

        private Task<LogResponseStatus> SendAsync(LogEvent logEvent)
        {
            var payloadType = ResolvePayloadType(logEvent);

            return payloadType switch
            {
                LogDBPayloadType.Cache => _client.LogCacheAsync(ConvertToLogCache(logEvent)),
                LogDBPayloadType.Beat => _client.LogBeatAsync(ConvertToLogBeat(logEvent)),
                _ => _client.LogAsync(ConvertToLog(logEvent))
            };
        }

        /// <summary>
        /// Convert Serilog LogEvent to LogDB Log DTO
        /// </summary>
        private LogDBLog ConvertToLog(LogEvent logEvent)
        {
            var log = new LogDBLog
            {
                Guid = Guid.NewGuid().ToString(),
                Timestamp = logEvent.Timestamp.UtcDateTime,
                Message = RenderMessage(logEvent),
                Level = MapLogLevel(logEvent.Level),
                Application = _options.DefaultApplication ?? GetSourceContext(logEvent) ?? "Unknown",
                Environment = _options.DefaultEnvironment,
                Source = GetSourceContext(logEvent),
                Collection = _options.DefaultCollection,
                Label = new List<string>(),
                AttributesS = new Dictionary<string, string>(),
                AttributesN = new Dictionary<string, double>(),
                AttributesB = new Dictionary<string, bool>(),
                AttributesD = new Dictionary<string, DateTime>()
            };

            // Handle exception
            if (logEvent.Exception != null)
            {
                log.Exception = logEvent.Exception.GetType().FullName ?? logEvent.Exception.GetType().Name;
                log.StackTrace = logEvent.Exception.StackTrace;
                log.AttributesS["ExceptionMessage"] = logEvent.Exception.Message;
                
                if (logEvent.Exception.InnerException != null)
                {
                    log.AttributesS["InnerException"] = logEvent.Exception.InnerException.Message;
                }
            }

            // Extract properties
            ExtractProperties(logEvent, log);

            // Extract correlation/trace information
            ExtractCorrelationInfo(logEvent, log);

            // Extract HTTP context if available
            ExtractHttpContext(logEvent, log);

            return log;
        }

        /// <summary>
        /// Render the log message
        /// </summary>
        private string RenderMessage(LogEvent logEvent)
        {
            if (_formatter != null)
            {
                using var writer = new StringWriter();
                _formatter.Format(logEvent, writer);
                return writer.ToString();
            }

            return logEvent.RenderMessage(_options.FormatProvider);
        }

        /// <summary>
        /// Map Serilog log level to LogDB log level
        /// </summary>
        private static LogDBLogLevel MapLogLevel(LogEventLevel level)
        {
            return level switch
            {
                LogEventLevel.Verbose => LogDBLogLevel.Debug,
                LogEventLevel.Debug => LogDBLogLevel.Debug,
                LogEventLevel.Information => LogDBLogLevel.Info,
                LogEventLevel.Warning => LogDBLogLevel.Warning,
                LogEventLevel.Error => LogDBLogLevel.Error,
                LogEventLevel.Fatal => LogDBLogLevel.Critical,
                _ => LogDBLogLevel.Info
            };
        }

        /// <summary>
        /// Get source context from log event properties
        /// </summary>
        private static string? GetSourceContext(LogEvent logEvent)
        {
            if (TryGetProperty(logEvent, "SourceContext", out var sourceContext))
            {
                return sourceContext.ToString().Trim('"');
            }
            return null;
        }

        /// <summary>
        /// Extract properties from log event to LogDB attributes
        /// </summary>
        private void ExtractProperties(LogEvent logEvent, LogDBLog log)
        {
            foreach (var property in logEvent.Properties)
            {
                var key = property.Key;

                // Skip special properties that are handled separately
                if (key == "SourceContext" || 
                    key == "RequestId" || 
                    key == "CorrelationId" ||
                    key == "TraceId" ||
                    key == "SpanId" ||
                    key == "RequestPath" ||
                    key == "HttpMethod" ||
                    key == "StatusCode" ||
                    key == "IpAddress" ||
                    key == "UserEmail" ||
                    key == "UserId" ||
                    IsControlProperty(key) ||
                    key.StartsWith("Tag.", StringComparison.OrdinalIgnoreCase) ||
                    key.StartsWith("Field.", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // Handle structured properties
                ExtractPropertyValue(log, key, property.Value);
            }
        }

        /// <summary>
        /// Extract a property value and add it to the appropriate attribute dictionary
        /// </summary>
        private void ExtractPropertyValue(LogDBLog log, string key, LogEventPropertyValue value)
        {
            switch (value)
            {
                case ScalarValue scalar:
                    AddScalarValue(log, key, scalar.Value);
                    break;

                case SequenceValue sequence:
                    // Convert sequence to JSON string
                    log.AttributesS[key] = JsonSerializer.Serialize(
                        sequence.Elements.Select(e => e.ToString()).ToArray()
                    );
                    break;

                case StructureValue structure:
                    // Convert structure to JSON string
                    var dict = structure.Properties.ToDictionary(
                        p => p.Name,
                        p => p.Value.ToString()
                    );
                    log.AttributesS[key] = JsonSerializer.Serialize(dict);
                    break;

                case DictionaryValue dictionary:
                    // Convert dictionary to JSON string
                    var dict2 = dictionary.Elements.ToDictionary(
                        kvp => kvp.Key.Value?.ToString() ?? "null",
                        kvp => kvp.Value.ToString()
                    );
                    log.AttributesS[key] = JsonSerializer.Serialize(dict2);
                    break;

                default:
                    log.AttributesS[key] = value.ToString();
                    break;
            }
        }

        /// <summary>
        /// Add a scalar value to the appropriate attribute dictionary
        /// </summary>
        private void AddScalarValue(LogDBLog log, string key, object? value)
        {
            if (value == null)
                return;

            switch (value)
            {
                case string stringValue:
                    log.AttributesS[key] = stringValue;
                    break;

                case bool boolValue:
                    log.AttributesB[key] = boolValue;
                    break;

                case byte byteValue:
                    log.AttributesN[key] = byteValue;
                    break;

                case sbyte sbyteValue:
                    log.AttributesN[key] = sbyteValue;
                    break;

                case short shortValue:
                    log.AttributesN[key] = shortValue;
                    break;

                case ushort ushortValue:
                    log.AttributesN[key] = ushortValue;
                    break;

                case int intValue:
                    log.AttributesN[key] = intValue;
                    break;

                case uint uintValue:
                    log.AttributesN[key] = uintValue;
                    break;

                case long longValue:
                    log.AttributesN[key] = longValue;
                    break;

                case ulong ulongValue:
                    log.AttributesN[key] = ulongValue;
                    break;

                case float floatValue:
                    log.AttributesN[key] = floatValue;
                    break;

                case double doubleValue:
                    log.AttributesN[key] = doubleValue;
                    break;

                case decimal decimalValue:
                    log.AttributesN[key] = (double)decimalValue;
                    break;

                case DateTime dateTimeValue:
                    log.AttributesD[key] = dateTimeValue;
                    break;

                case DateTimeOffset dateTimeOffsetValue:
                    log.AttributesD[key] = dateTimeOffsetValue.UtcDateTime;
                    break;

                case TimeSpan timeSpanValue:
                    log.AttributesN[key] = timeSpanValue.TotalMilliseconds;
                    break;

                case Guid guidValue:
                    log.AttributesS[key] = guidValue.ToString();
                    break;

                default:
                    log.AttributesS[key] = value.ToString() ?? string.Empty;
                    break;
            }
        }

        /// <summary>
        /// Extract correlation and trace information
        /// </summary>
        private void ExtractCorrelationInfo(LogEvent logEvent, LogDBLog log)
        {
            // Check for CorrelationId in properties
            if (TryGetProperty(logEvent, "CorrelationId", out var correlationId))
            {
                log.CorrelationId = correlationId.ToString().Trim('"');
            }
            else if (TryGetProperty(logEvent, "RequestId", out var requestId))
            {
                log.CorrelationId = requestId.ToString().Trim('"');
            }

            // Check for TraceId and SpanId
            if (TryGetProperty(logEvent, "TraceId", out var traceId))
            {
                log.AttributesS["TraceId"] = traceId.ToString().Trim('"');
            }

            if (TryGetProperty(logEvent, "SpanId", out var spanId))
            {
                log.AttributesS["SpanId"] = spanId.ToString().Trim('"');
            }

            // Use Activity.Current if available
            var activity = Activity.Current;
            if (activity != null)
            {
                if (string.IsNullOrEmpty(log.CorrelationId))
                {
                    log.CorrelationId = activity.Id;
                }

                if (!log.AttributesS.ContainsKey("TraceId"))
                {
                    log.AttributesS["TraceId"] = activity.TraceId.ToString();
                }

                if (!log.AttributesS.ContainsKey("SpanId"))
                {
                    log.AttributesS["SpanId"] = activity.SpanId.ToString();
                }

                if (!string.IsNullOrEmpty(activity.ParentId))
                {
                    log.AttributesS["ParentSpanId"] = activity.ParentId!;
                }

                // Add activity tags
                foreach (var tag in activity.Tags)
                {
                    if (!log.AttributesS.ContainsKey($"Activity.{tag.Key}"))
                    {
                        log.AttributesS[$"Activity.{tag.Key}"] = tag.Value ?? string.Empty;
                    }
                }
            }
        }

        /// <summary>
        /// Extract HTTP context information
        /// </summary>
        private void ExtractHttpContext(LogEvent logEvent, LogDBLog log)
        {
            if (TryGetProperty(logEvent, "RequestPath", out var requestPath))
            {
                log.RequestPath = requestPath.ToString().Trim('"');
            }

            if (TryGetProperty(logEvent, "HttpMethod", out var httpMethod))
            {
                log.HttpMethod = httpMethod.ToString().Trim('"');
            }

            if (TryGetProperty(logEvent, "StatusCode", out var statusCode))
            {
                if (int.TryParse(statusCode.ToString().Trim('"'), out var code))
                {
                    log.StatusCode = code;
                }
            }

            if (TryGetProperty(logEvent, "IpAddress", out var ipAddress))
            {
                log.IpAddress = ipAddress.ToString().Trim('"');
            }

            if (TryGetProperty(logEvent, "UserEmail", out var userEmail))
            {
                log.UserEmail = userEmail.ToString().Trim('"');
            }

            if (TryGetProperty(logEvent, "UserId", out var userId))
            {
                if (int.TryParse(userId.ToString().Trim('"'), out var id))
                {
                    log.UserId = id;
                }
            }
        }

        private LogCache ConvertToLogCache(LogEvent logEvent)
        {
            var key = GetPropertyString(logEvent, "LogDBCacheKey");
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new InvalidOperationException("LogDB cache routing requires property LogDBCacheKey.");
            }

            var value = GetPropertyString(logEvent, "LogDBCacheValue") ?? RenderMessage(logEvent);
            var collection = GetPropertyString(logEvent, "LogDBCollection") ?? _options.DefaultCollection;

            var cacheKey = key!;

            var cache = new LogCache
            {
                Key = cacheKey,
                Value = value ?? string.Empty,
                Collection = collection,
                Timestamp = logEvent.Timestamp.UtcDateTime
            };

            if (TryGetIntProperty(logEvent, "LogDBTtlSeconds", out var ttlSeconds))
            {
                cache.TtlSeconds = ttlSeconds;
            }

            return cache;
        }

        private LogBeat ConvertToLogBeat(LogEvent logEvent)
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
                Collection = GetPropertyString(logEvent, "LogDBCollection") ?? _options.DefaultCollection,
                Timestamp = logEvent.Timestamp.UtcDateTime
            };

            beat.Application = GetPropertyString(logEvent, "LogDBApplication")
                ?? _options.DefaultApplication
                ?? GetSourceContext(logEvent);
            beat.Environment = GetPropertyString(logEvent, "LogDBEnvironment") ?? _options.DefaultEnvironment;

            if (TryGetProperty(logEvent, "LogDBTags", out var tagsValue))
            {
                foreach (var item in EnumerateMetaEntries(tagsValue))
                {
                    AddOrUpdateMeta(beat.Tag, item.Key, item.Value);
                }
            }

            if (TryGetProperty(logEvent, "LogDBFields", out var fieldsValue))
            {
                foreach (var item in EnumerateMetaEntries(fieldsValue))
                {
                    AddOrUpdateMeta(beat.Field, item.Key, item.Value);
                }
            }

            foreach (var property in logEvent.Properties)
            {
                if (property.Key.StartsWith("Tag.", StringComparison.OrdinalIgnoreCase))
                {
                    AddOrUpdateMeta(beat.Tag, property.Key.Substring("Tag.".Length), GetValueAsString(property.Value));
                    continue;
                }

                if (property.Key.StartsWith("Field.", StringComparison.OrdinalIgnoreCase))
                {
                    AddOrUpdateMeta(beat.Field, property.Key.Substring("Field.".Length), GetValueAsString(property.Value));
                }
            }

            return beat;
        }

        private static bool TryGetProperty(LogEvent logEvent, string key, out LogEventPropertyValue value)
        {
            if (logEvent.Properties.TryGetValue(key, out value!))
            {
                return true;
            }

            foreach (var property in logEvent.Properties)
            {
                if (string.Equals(property.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }

            value = null!;
            return false;
        }

        private static string? GetPropertyString(LogEvent logEvent, string key)
        {
            if (!TryGetProperty(logEvent, key, out var value))
            {
                return null;
            }

            var text = GetValueAsString(value);
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }

        private static bool TryGetIntProperty(LogEvent logEvent, string key, out int value)
        {
            value = default;
            if (!TryGetProperty(logEvent, key, out var propertyValue))
            {
                return false;
            }

            if (propertyValue is ScalarValue scalar && scalar.Value is IConvertible convertible)
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

            return int.TryParse(GetValueAsString(propertyValue), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        private static string GetValueAsString(LogEventPropertyValue value)
        {
            return value switch
            {
                ScalarValue scalar when scalar.Value == null => string.Empty,
                ScalarValue scalar => Convert.ToString(scalar.Value, CultureInfo.InvariantCulture) ?? string.Empty,
                _ => value.ToString().Trim('"')
            };
        }

        private static IEnumerable<KeyValuePair<string, string>> EnumerateMetaEntries(LogEventPropertyValue value)
        {
            if (value is DictionaryValue dictionary)
            {
                foreach (var entry in dictionary.Elements)
                {
                    var key = entry.Key.Value?.ToString();
                    if (!string.IsNullOrWhiteSpace(key))
                    {
                        yield return new KeyValuePair<string, string>(key!, GetValueAsString(entry.Value));
                    }
                }

                yield break;
            }

            if (value is StructureValue structure)
            {
                foreach (var property in structure.Properties)
                {
                    if (!string.IsNullOrWhiteSpace(property.Name))
                    {
                        yield return new KeyValuePair<string, string>(property.Name, GetValueAsString(property.Value));
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

        private LogDBPayloadType ResolvePayloadType(LogEvent logEvent)
        {
            if (TryGetProperty(logEvent, "LogDBType", out var rawValue))
            {
                if (TryParsePayloadType(rawValue, out var payloadType))
                {
                    return payloadType;
                }

                throw new InvalidOperationException(
                    $"Invalid LogDBType '{GetValueAsString(rawValue)}'. Use " +
                    $"{nameof(LogDBPayloadType)}.{nameof(LogDBPayloadType.Log)}, " +
                    $"{nameof(LogDBPayloadType)}.{nameof(LogDBPayloadType.Cache)}, or " +
                    $"{nameof(LogDBPayloadType)}.{nameof(LogDBPayloadType.Beat)}.");
            }

            if (_options.DefaultPayloadType.HasValue)
            {
                return _options.DefaultPayloadType.Value;
            }

            throw new InvalidOperationException(
                "LogDBType is required for each Serilog event. Set the LogDBType property to a LogDBPayloadType value, " +
                "or configure options.DefaultPayloadType.");
        }

        private static bool TryParsePayloadType(LogEventPropertyValue value, out LogDBPayloadType payloadType)
        {
            if (value is ScalarValue scalar)
            {
                if (scalar.Value is LogDBPayloadType typedEnum)
                {
                    payloadType = typedEnum;
                    return true;
                }
            }

            payloadType = default;
            return false;
        }

        private static bool IsControlProperty(string key)
        {
            return string.Equals(key, "LogDBType", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(key, "LogDBCollection", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(key, "LogDBCacheKey", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(key, "LogDBCacheValue", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(key, "LogDBTtlSeconds", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(key, "LogDBMeasurement", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(key, "LogDBApplication", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(key, "LogDBEnvironment", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(key, "LogDBTags", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(key, "LogDBFields", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Flush any pending logs
        /// </summary>
        public async Task FlushAsync(CancellationToken cancellationToken = default)
        {
            if (_disposed)
                return;

            try
            {
                await _client.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error flushing LogDB sink");
            }
        }

        /// <summary>
        /// Dispose the sink
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            try
            {
                // Flush any pending logs
                FlushAsync().GetAwaiter().GetResult();

                // Dispose the client
                if (_client is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error disposing LogDB sink");
            }
            finally
            {
                _disposed = true;
            }
        }
    }
}
