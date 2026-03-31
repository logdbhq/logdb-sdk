using System;
using System.Collections.Generic;
using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Logs;
using LogDB.Client.Models;
using LogDB.Extensions.Logging;

namespace LogDB.OpenTelemetry
{
    /// <summary>
    /// OpenTelemetry log exporter for LogDB
    /// </summary>
    public class LogDBLogExporter : BaseExporter<global::OpenTelemetry.Logs.LogRecord>
    {
        private readonly LogDBExporterOptions _options;
        private readonly ILogDBClient _client;

        public LogDBLogExporter(LogDBExporterOptions options, ILogDBClient? client = null)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _client = client ?? CreateClient(options);
        }

        private static ILogDBClient CreateClient(LogDBExporterOptions options)
        {
            var loggerOptions = new LogDBLoggerOptions
            {
                ApiKey = options.ApiKey,
                ServiceUrl = options.Endpoint,
                Protocol = options.Protocol,
                DefaultCollection = options.DefaultCollection ?? "logs",
                EnableBatching = options.ExportProcessorType == ExportProcessorType.Batch,
                BatchSize = options.BatchExportProcessorOptions?.MaxExportBatchSize ?? 512,
                FlushInterval = TimeSpan.FromMilliseconds(
                    options.BatchExportProcessorOptions?.ScheduledDelayMilliseconds ?? 1000),
                Headers = options.Headers
            };

            return new LogDBClient(
                Microsoft.Extensions.Options.Options.Create(loggerOptions));
        }

        public override ExportResult Export(in Batch<global::OpenTelemetry.Logs.LogRecord> batch)
        {
            var identity = _options.ResolveIdentity();
            var serviceName = identity.ServiceName;
            var environment = identity.Environment;
            var collection = LogDBExporterHelpers.ResolveCollection(_options, "logs");

            try
            {
                var logs = new List<Log>();

                foreach (var logRecord in batch)
                {
                    var log = ConvertLogRecordToLog(logRecord, serviceName, environment, collection);
                    logs.Add(log);
                }

                if (logs.Count > 0)
                {
                    System.Threading.Tasks.Task<LogResponseStatus> task;
                    if (_options.ExportProcessorType == ExportProcessorType.Batch)
                    {
                        task = _client.SendLogBatchAsync(logs);
                    }
                    else
                    {
                        task = SendLogsIndividuallyAsync(logs);
                    }

                    if (!LogDBExporterHelpers.TryWaitForStatus(task, _options, "log export", out _))
                    {
                        return ExportResult.Failure;
                    }
                }

                return ExportResult.Success;
            }
            catch (Exception ex)
            {
                _options.ReportError(ex, "log export");
                return ExportResult.Failure;
            }
        }

        protected override bool OnShutdown(int timeoutMilliseconds)
        {
            try
            {
                return LogDBExporterHelpers.TryWait(
                    _client.FlushAsync(),
                    timeoutMilliseconds,
                    _options,
                    "log exporter shutdown flush");
            }
            catch (Exception ex)
            {
                _options.ReportError(ex, "log exporter shutdown");
                return false;
            }
        }

        private async System.Threading.Tasks.Task<LogResponseStatus> SendLogsIndividuallyAsync(IReadOnlyList<Log> logs)
        {
            foreach (var log in logs)
            {
                var status = await _client.LogAsync(log).ConfigureAwait(false);
                if (status != LogResponseStatus.Success)
                {
                    _options.WriteDebug($"Individual log export returned status '{status}'.");
                    return status;
                }
            }

            return LogResponseStatus.Success;
        }

        private Log ConvertLogRecordToLog(
            global::OpenTelemetry.Logs.LogRecord logRecord,
            string serviceName,
            string environment,
            string collection)
        {
            var traceId = logRecord.TraceId.ToString();
            var spanId = logRecord.SpanId.ToString();
            var hasTraceContext = traceId != "00000000000000000000000000000000" &&
                                  spanId != "0000000000000000";

            var log = new Log
            {
                Guid = Guid.NewGuid().ToString(),
                ApiKey = _options.ApiKey,
                Timestamp = logRecord.Timestamp,
                Message = logRecord.FormattedMessage ?? string.Empty,
                Level = MapLogLevel(logRecord.LogLevel),
                Application = serviceName,
                Environment = environment,
                Source = logRecord.CategoryName ?? string.Empty,
                Collection = collection,
                AttributesD = new Dictionary<string, DateTime>(),
                AttributesN = new Dictionary<string, double>(),
                AttributesS = new Dictionary<string, string>(),
                AttributesB = new Dictionary<string, bool>(),
                Label = new List<string>()
            };

            if (hasTraceContext)
            {
                log.CorrelationId = traceId;
                log.AttributesS["trace.id"] = traceId;
                log.AttributesS["span.id"] = spanId;
                log.AttributesS["trace.flags"] = logRecord.TraceFlags.ToString();
            }

            log.AttributesS["severity.text"] = logRecord.LogLevel.ToString();
            log.AttributesN["severity.number"] = GetSeverityNumber(logRecord.LogLevel);

            if (logRecord.EventId != default)
            {
                log.AttributesN["event.id"] = logRecord.EventId.Id;
                if (!string.IsNullOrEmpty(logRecord.EventId.Name))
                {
                    log.AttributesS["event.name"] = logRecord.EventId.Name;
                }
            }

            if (logRecord.Exception != null)
            {
                log.Exception = logRecord.Exception.ToString();
                log.StackTrace = logRecord.Exception.StackTrace;
                log.AttributesS["exception.type"] = logRecord.Exception.GetType().FullName ?? "UnknownException";
                log.AttributesS["exception.message"] = logRecord.Exception.Message;
            }

            if (logRecord.Attributes != null)
            {
                foreach (var attribute in logRecord.Attributes)
                {
                    LogDBExporterHelpers.AddAttribute(log, attribute.Key, attribute.Value);
                }
            }

            var scopeIndex = 0;
            logRecord.ForEachScope((scope, logEntry) =>
            {
                var hasStructuredScopeValues = false;
                foreach (var scopeItem in scope)
                {
                    hasStructuredScopeValues = true;
                    LogDBExporterHelpers.AddAttribute(
                        logEntry,
                        $"scope.{scopeIndex}.{scopeItem.Key}",
                        scopeItem.Value);
                }

                if (!hasStructuredScopeValues && scope.Scope != null)
                {
                    LogDBExporterHelpers.AddAttribute(logEntry, $"scope.{scopeIndex}", scope.Scope);
                }

                scopeIndex++;
            }, log);

            if (!hasTraceContext && Activity.Current != null)
            {
                var activity = Activity.Current;
                log.CorrelationId = activity.TraceId.ToString();
                log.AttributesS["trace.id"] = activity.TraceId.ToString();
                log.AttributesS["span.id"] = activity.SpanId.ToString();
            }

            return log;
        }

        private int GetSeverityNumber(Microsoft.Extensions.Logging.LogLevel logLevel)
        {
            return logLevel switch
            {
                Microsoft.Extensions.Logging.LogLevel.Trace => 1,
                Microsoft.Extensions.Logging.LogLevel.Debug => 5,
                Microsoft.Extensions.Logging.LogLevel.Information => 9,
                Microsoft.Extensions.Logging.LogLevel.Warning => 13,
                Microsoft.Extensions.Logging.LogLevel.Error => 17,
                Microsoft.Extensions.Logging.LogLevel.Critical => 21,
                _ => 0
            };
        }

        private LogLevel MapLogLevel(Microsoft.Extensions.Logging.LogLevel logLevel)
        {
            return logLevel switch
            {
                Microsoft.Extensions.Logging.LogLevel.Trace => LogLevel.Debug,
                Microsoft.Extensions.Logging.LogLevel.Debug => LogLevel.Debug,
                Microsoft.Extensions.Logging.LogLevel.Information => LogLevel.Info,
                Microsoft.Extensions.Logging.LogLevel.Warning => LogLevel.Warning,
                Microsoft.Extensions.Logging.LogLevel.Error => LogLevel.Error,
                Microsoft.Extensions.Logging.LogLevel.Critical => LogLevel.Critical,
                _ => LogLevel.Info
            };
        }
    }
}
