using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using NLog;
using LogDB.Client.Models;
using LogDBLog = LogDB.Client.Models.Log;
using LogDBLogLevel = LogDB.Client.Models.LogLevel;

namespace LogDB.NLog
{
    /// <summary>
    /// Converts NLog LogEventInfo to LogDB Log DTO
    /// </summary>
    internal class LogEventInfoConverter
    {
        private readonly LogDBTargetOptions _options;

        public LogEventInfoConverter(LogDBTargetOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        /// <summary>
        /// Convert NLog LogEventInfo to LogDB Log DTO
        /// </summary>
        public LogDBLog Convert(LogEventInfo logEvent)
        {
            var log = new LogDBLog
            {
                Guid = Guid.NewGuid().ToString(),
                Timestamp = logEvent.TimeStamp.ToUniversalTime(),
                Message = logEvent.FormattedMessage ?? logEvent.Message,
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
        /// Map NLog log level to LogDB log level
        /// NLog.LogLevel is a class (not an enum), so we compare by reference or ordinal
        /// </summary>
        private static LogDBLogLevel MapLogLevel(global::NLog.LogLevel level)
        {
            if (level == null)
                return LogDBLogLevel.Info;

            // Compare by reference first (fastest for standard levels)
            if (ReferenceEquals(level, global::NLog.LogLevel.Trace) || ReferenceEquals(level, global::NLog.LogLevel.Debug))
                return LogDBLogLevel.Debug;
            if (ReferenceEquals(level, global::NLog.LogLevel.Info))
                return LogDBLogLevel.Info;
            if (ReferenceEquals(level, global::NLog.LogLevel.Warn))
                return LogDBLogLevel.Warning;
            if (ReferenceEquals(level, global::NLog.LogLevel.Error))
                return LogDBLogLevel.Error;
            if (ReferenceEquals(level, global::NLog.LogLevel.Fatal))
                return LogDBLogLevel.Critical;

            // Fallback: compare by ordinal value for custom levels
            var ordinal = level.Ordinal;
            if (ordinal <= global::NLog.LogLevel.Debug.Ordinal)
                return LogDBLogLevel.Debug;
            if (ordinal <= global::NLog.LogLevel.Info.Ordinal)
                return LogDBLogLevel.Info;
            if (ordinal <= global::NLog.LogLevel.Warn.Ordinal)
                return LogDBLogLevel.Warning;
            if (ordinal <= global::NLog.LogLevel.Error.Ordinal)
                return LogDBLogLevel.Error;

            return LogDBLogLevel.Critical;
        }

        /// <summary>
        /// Get source context from log event properties
        /// </summary>
        private static string? GetSourceContext(LogEventInfo logEvent)
        {
            if (logEvent.Properties.TryGetValue("SourceContext", out var sourceContext))
            {
                return sourceContext?.ToString();
            }

            // Also check LoggerName
            if (!string.IsNullOrEmpty(logEvent.LoggerName))
            {
                return logEvent.LoggerName;
            }

            return null;
        }

        /// <summary>
        /// Extract properties from log event to LogDB attributes
        /// </summary>
        private void ExtractProperties(LogEventInfo logEvent, LogDBLog log)
        {
            foreach (var property in logEvent.Properties)
            {
                var key = property.Key?.ToString();
                if (string.IsNullOrEmpty(key))
                {
                    continue;
                }

                var propertyKey = key!;

                // Skip special properties that are handled separately
                if (string.Equals(propertyKey, "SourceContext", StringComparison.OrdinalIgnoreCase) || 
                    string.Equals(propertyKey, "RequestId", StringComparison.OrdinalIgnoreCase) || 
                    string.Equals(propertyKey, "CorrelationId", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(propertyKey, "TraceId", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(propertyKey, "SpanId", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(propertyKey, "RequestPath", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(propertyKey, "HttpMethod", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(propertyKey, "StatusCode", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(propertyKey, "IpAddress", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(propertyKey, "UserEmail", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(propertyKey, "UserId", StringComparison.OrdinalIgnoreCase) ||
                    IsControlProperty(propertyKey) ||
                    propertyKey.StartsWith("Tag.", StringComparison.OrdinalIgnoreCase) ||
                    propertyKey.StartsWith("Field.", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // Handle property value
                ExtractPropertyValue(log, propertyKey, property.Value);
            }
        }

        /// <summary>
        /// Extract a property value and add it to the appropriate attribute dictionary
        /// </summary>
        private void ExtractPropertyValue(LogDBLog log, string key, object? value)
        {
            if (value == null)
                return;

            // Handle different types
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

                case System.Collections.IEnumerable enumerable:
                    // Convert collections to JSON string
                    try
                    {
                        var items = enumerable.Cast<object>().Select(x => x?.ToString() ?? "null").ToArray();
                        log.AttributesS[key] = JsonSerializer.Serialize(items);
                    }
                    catch
                    {
                        log.AttributesS[key] = value.ToString() ?? string.Empty;
                    }
                    break;

                default:
                    // For complex objects, try to serialize to JSON
                    try
                    {
                        log.AttributesS[key] = JsonSerializer.Serialize(value);
                    }
                    catch
                    {
                        // Fallback to ToString()
                        log.AttributesS[key] = value.ToString() ?? string.Empty;
                    }
                    break;
            }
        }

        /// <summary>
        /// Extract correlation and trace information
        /// </summary>
        private void ExtractCorrelationInfo(LogEventInfo logEvent, LogDBLog log)
        {
            // Check for CorrelationId in properties
            if (logEvent.Properties.TryGetValue("CorrelationId", out var correlationId))
            {
                log.CorrelationId = correlationId?.ToString();
            }
            else if (logEvent.Properties.TryGetValue("RequestId", out var requestId))
            {
                log.CorrelationId = requestId?.ToString();
            }

            // Check for TraceId and SpanId
            if (logEvent.Properties.TryGetValue("TraceId", out var traceId))
            {
                log.AttributesS["TraceId"] = traceId?.ToString() ?? string.Empty;
            }

            if (logEvent.Properties.TryGetValue("SpanId", out var spanId))
            {
                log.AttributesS["SpanId"] = spanId?.ToString() ?? string.Empty;
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
        private void ExtractHttpContext(LogEventInfo logEvent, LogDBLog log)
        {
            if (logEvent.Properties.TryGetValue("RequestPath", out var requestPath))
            {
                log.RequestPath = requestPath?.ToString();
            }

            if (logEvent.Properties.TryGetValue("HttpMethod", out var httpMethod))
            {
                log.HttpMethod = httpMethod?.ToString();
            }

            if (logEvent.Properties.TryGetValue("StatusCode", out var statusCode))
            {
                if (int.TryParse(statusCode?.ToString(), out var code))
                {
                    log.StatusCode = code;
                }
            }

            if (logEvent.Properties.TryGetValue("IpAddress", out var ipAddress))
            {
                log.IpAddress = ipAddress?.ToString();
            }

            if (logEvent.Properties.TryGetValue("UserEmail", out var userEmail))
            {
                log.UserEmail = userEmail?.ToString();
            }

            if (logEvent.Properties.TryGetValue("UserId", out var userId))
            {
                if (int.TryParse(userId?.ToString(), out var id))
                {
                    log.UserId = id;
                }
            }
        }

        private static bool IsControlProperty(string key)
        {
            return key.Equals("LogDBType", StringComparison.OrdinalIgnoreCase) ||
                   key.Equals("LogDBCollection", StringComparison.OrdinalIgnoreCase) ||
                   key.Equals("LogDBCacheKey", StringComparison.OrdinalIgnoreCase) ||
                   key.Equals("LogDBCacheValue", StringComparison.OrdinalIgnoreCase) ||
                   key.Equals("LogDBTtlSeconds", StringComparison.OrdinalIgnoreCase) ||
                   key.Equals("LogDBMeasurement", StringComparison.OrdinalIgnoreCase) ||
                   key.Equals("LogDBApplication", StringComparison.OrdinalIgnoreCase) ||
                   key.Equals("LogDBEnvironment", StringComparison.OrdinalIgnoreCase) ||
                   key.Equals("LogDBTags", StringComparison.OrdinalIgnoreCase) ||
                   key.Equals("LogDBFields", StringComparison.OrdinalIgnoreCase);
        }
    }
}






