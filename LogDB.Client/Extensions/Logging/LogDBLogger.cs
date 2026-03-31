using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using LogDB.Client.Models;

namespace LogDB.Extensions.Logging
{
    /// <summary>
    /// ILogger implementation that sends logs to LogDB
    /// </summary>
    public class LogDBLogger : ILogger
    {
        private readonly string _categoryName;
        private readonly LogDBLoggerOptions _options;
        private readonly ILogDBClient _client;
        private readonly IExternalScopeProvider _scopeProvider;

        public LogDBLogger(
            string categoryName,
            LogDBLoggerOptions options,
            ILogDBClient client,
            IExternalScopeProvider scopeProvider)
        {
            _categoryName = categoryName;
            _options = options;
            _client = client;
            _scopeProvider = scopeProvider;
        }

            public IDisposable BeginScope<TState>(TState state) where TState : notnull
    {
        return _scopeProvider?.Push(state) ?? NullScope.Instance;
    }

            public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel)
    {
        return logLevel != Microsoft.Extensions.Logging.LogLevel.None && 
               logLevel >= _options.MinimumLevel &&
               (_options.Filter?.Invoke(_categoryName, logLevel) ?? true);
    }

    public void Log<TState>(
        Microsoft.Extensions.Logging.LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            // Skip if sampling is enabled and this log should be sampled out
            if (_options.EnableSampling && !ShouldLog(logLevel))
                return;

            var logEntry = CreateLogEntry(logLevel, eventId, state, exception, formatter);
            
            try
            {
                var sendTask = _client.LogAsync(logEntry);
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
                _options.OnError?.Invoke(ex, new[] { logEntry });
            }
        }

            private Log CreateLogEntry<TState>(
        Microsoft.Extensions.Logging.LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
        {
            var message = formatter(state, exception);
            var timestamp = DateTimeOffset.UtcNow;
            
            var log = new Log
            {
                Guid = Guid.NewGuid().ToString(),
                Timestamp = timestamp.UtcDateTime,
                Message = message,
                Level = MapLogLevel(logLevel),
                Application = _options.DefaultApplication ?? _categoryName,
                Environment = _options.DefaultEnvironment ?? "production",
                Source = _categoryName,
                Collection = _options.DefaultCollection ?? "logs",
                ApiKey = _options.ApiKey,
                Label = new List<string>(),
                AttributesS = new Dictionary<string, string>(),
                AttributesN = new Dictionary<string, double>(),
                AttributesB = new Dictionary<string, bool>(),
                AttributesD = new Dictionary<string, DateTime>()
            };

            // Add event ID if present
            if (eventId.Id != 0)
            {
                log.AttributesN["EventId"] = eventId.Id;
                if (!string.IsNullOrEmpty(eventId.Name))
                {
                    log.AttributesS["EventName"] = eventId.Name;
                }
            }

            // Add exception details
            if (exception != null)
            {
                log.Exception = exception.GetType().FullName;
                log.StackTrace = exception.StackTrace;
                log.AttributesS["ExceptionMessage"] = exception.Message;
                
                if (exception.InnerException != null)
                {
                    log.AttributesS["InnerException"] = exception.InnerException.Message;
                }
            }

            // Extract structured logging properties
            if (state is IReadOnlyList<KeyValuePair<string, object>> values)
            {
                foreach (var kvp in values)
                {
                    if (kvp.Key == "{OriginalFormat}")
                    {
                        log.AttributesS["MessageTemplate"] = kvp.Value?.ToString() ?? "";
                        continue;
                    }

                    AddProperty(log, kvp.Key, kvp.Value);
                }
            }

            // Add scopes if enabled
            if (_options.IncludeScopes && _scopeProvider != null)
            {
                _scopeProvider.ForEachScope((scope, logEntry) =>
                {
                    if (scope is IEnumerable<KeyValuePair<string, object?>> scopeItems)
                    {
                        foreach (var kvp in scopeItems)
                        {
                            AddProperty(logEntry, $"Scope.{kvp.Key}", kvp.Value);
                        }
                    }
                    else if (scope != null)
                    {
                        var scopeString = scope.ToString();
                        if (!string.IsNullOrEmpty(scopeString))
                        {
                            logEntry.AttributesS["Scope"] = scopeString;
                        }
                    }
                }, log);
            }

            // Add activity (correlation) information
            var activity = Activity.Current;
            if (activity != null)
            {
                log.CorrelationId = activity.Id;
                log.AttributesS["TraceId"] = activity.TraceId.ToString();
                log.AttributesS["SpanId"] = activity.SpanId.ToString();
                
                if (!string.IsNullOrEmpty(activity.ParentId))
                {
                    log.AttributesS["ParentSpanId"] = activity.ParentId;
                }

                foreach (var tag in activity.Tags)
                {
                    AddProperty(log, $"Activity.{tag.Key}", tag.Value);
                }
            }

            // Apply enrichers
            foreach (var enricher in _options.Enrichers)
            {
                enricher.Enrich(log);
            }

            return log;
        }

        private void AddProperty(Log log, string key, object? value)
        {
            if (value == null) return;

            switch (value)
            {
                case string stringValue:
                    log.AttributesS[key] = stringValue;
                    break;
                case bool boolValue:
                    log.AttributesB[key] = boolValue;
                    break;
                case int intValue:
                    log.AttributesN[key] = intValue;
                    break;
                case long longValue:
                    log.AttributesN[key] = longValue;
                    break;
                case double doubleValue:
                    log.AttributesN[key] = doubleValue;
                    break;
                case float floatValue:
                    log.AttributesN[key] = floatValue;
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
                default:
                    var textValue = value.ToString();
                    if (!string.IsNullOrEmpty(textValue))
                    {
                        log.AttributesS[key] = textValue;
                    }
                    break;
            }
        }

        private LogDB.Client.Models.LogLevel MapLogLevel(Microsoft.Extensions.Logging.LogLevel logLevel)
        {
            return logLevel switch
            {
                Microsoft.Extensions.Logging.LogLevel.Trace => LogDB.Client.Models.LogLevel.Debug,
                Microsoft.Extensions.Logging.LogLevel.Debug => LogDB.Client.Models.LogLevel.Debug,
                Microsoft.Extensions.Logging.LogLevel.Information => LogDB.Client.Models.LogLevel.Info,
                Microsoft.Extensions.Logging.LogLevel.Warning => LogDB.Client.Models.LogLevel.Warning,
                Microsoft.Extensions.Logging.LogLevel.Error => LogDB.Client.Models.LogLevel.Error,
                Microsoft.Extensions.Logging.LogLevel.Critical => LogDB.Client.Models.LogLevel.Critical,
                _ => LogDB.Client.Models.LogLevel.Info
            };
        }

            private bool ShouldLog(Microsoft.Extensions.Logging.LogLevel logLevel)
    {
        // Always log errors and above
        if (_options.AlwaysIncludeErrors && logLevel >= Microsoft.Extensions.Logging.LogLevel.Error)
                return true;

            // Simple random sampling
#if NETFRAMEWORK
            return new Random().NextDouble() <= _options.SamplingRate;
#else
            return Random.Shared.NextDouble() <= _options.SamplingRate;
#endif
        }

        private class NullScope : IDisposable
        {
            public static NullScope Instance { get; } = new NullScope();
            public void Dispose() { }
        }
    }
}
