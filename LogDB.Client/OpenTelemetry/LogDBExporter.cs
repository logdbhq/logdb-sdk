using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using OpenTelemetry;
using OpenTelemetry.Trace;
using LogDB.Client.Models;
using LogDB.Extensions.Logging;

namespace LogDB.OpenTelemetry
{
    /// <summary>
    /// OpenTelemetry exporter for LogDB
    /// </summary>
    public class LogDBTraceExporter : BaseExporter<Activity>
    {
        private readonly LogDBExporterOptions _options;
        private readonly ILogDBClient _client;

        public LogDBTraceExporter(LogDBExporterOptions options, ILogDBClient? client = null)
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
                DefaultCollection = options.DefaultCollection ?? "traces",
                EnableBatching = options.ExportProcessorType == ExportProcessorType.Batch,
                BatchSize = options.BatchExportProcessorOptions?.MaxExportBatchSize ?? 512,
                FlushInterval = TimeSpan.FromMilliseconds(
                    options.BatchExportProcessorOptions?.ScheduledDelayMilliseconds ?? 5000),
                Headers = options.Headers
            };

            return new LogDBClient(
                Microsoft.Extensions.Options.Options.Create(loggerOptions));
        }

        public override ExportResult Export(in Batch<Activity> batch)
        {
            var identity = _options.ResolveIdentity();
            var serviceName = identity.ServiceName;
            var environment = identity.Environment;
            var collection = LogDBExporterHelpers.ResolveCollection(_options, "traces");

            try
            {
                var logs = new List<Log>();
                var relations = new List<LogRelation>();

                foreach (var activity in batch)
                {
                    logs.Add(ConvertActivityToLog(activity, serviceName, environment, collection));

                    var relation = ConvertParentRelation(activity, serviceName, environment, collection);
                    if (relation != null)
                    {
                        relations.Add(relation);
                    }
                }

                if (logs.Count > 0)
                {
                    System.Threading.Tasks.Task<LogResponseStatus> logTask;
                    if (_options.ExportProcessorType == ExportProcessorType.Batch)
                    {
                        logTask = _client.SendLogBatchAsync(logs);
                    }
                    else
                    {
                        logTask = SendLogsIndividuallyAsync(logs);
                    }

                    if (!LogDBExporterHelpers.TryWaitForStatus(logTask, _options, "trace log export", out _))
                    {
                        return ExportResult.Failure;
                    }
                }

                if (relations.Count > 0)
                {
                    _options.WriteDebug(
                        $"Trace relation export skipped: LogRelation track is marked [Soon]. Skipped {relations.Count} relation(s).");
                }

                return ExportResult.Success;
            }
            catch (Exception ex)
            {
                _options.ReportError(ex, "trace export");
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
                    "trace exporter shutdown flush");
            }
            catch (Exception ex)
            {
                _options.ReportError(ex, "trace exporter shutdown");
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
                    _options.WriteDebug($"Individual trace log export returned status '{status}'.");
                    return status;
                }
            }

            return LogResponseStatus.Success;
        }

        private Log ConvertActivityToLog(
            Activity activity,
            string serviceName,
            string environment,
            string collection)
        {
            var traceId = activity.TraceId.ToString();
            var spanId = activity.SpanId.ToString();
            var parentSpanId = GetParentSpanId(activity);
            var links = activity.Links.ToArray();
            var events = activity.Events.ToArray();

            var log = new Log
            {
                Guid = Guid.NewGuid().ToString(),
                ApiKey = _options.ApiKey,
                Timestamp = activity.StartTimeUtc,
                Message = $"Span '{activity.DisplayName}' ({activity.Kind})",
                Level = activity.Status == ActivityStatusCode.Error
                    ? LogLevel.Error
                    : LogLevel.Info,
                Application = serviceName,
                Environment = environment,
                Source = activity.Source.Name,
                CorrelationId = traceId,
                Collection = collection,
                Label = new List<string> { "trace", $"span:{spanId}" },
                AttributesS = new Dictionary<string, string>
                {
                    ["trace.id"] = traceId,
                    ["span.id"] = spanId,
                    ["span.name"] = activity.DisplayName,
                    ["span.kind"] = activity.Kind.ToString(),
                    ["span.status"] = activity.Status.ToString(),
                    ["trace.flags"] = activity.ActivityTraceFlags.ToString()
                },
                AttributesN = new Dictionary<string, double>
                {
                    ["duration.ms"] = activity.Duration.TotalMilliseconds,
                    ["span.events.count"] = events.Length,
                    ["span.links.count"] = links.Length
                },
                AttributesB = new Dictionary<string, bool>(),
                AttributesD = new Dictionary<string, DateTime>
                {
                    ["span.start"] = activity.StartTimeUtc,
                    ["span.end"] = activity.StartTimeUtc.Add(activity.Duration)
                }
            };

            if (!string.IsNullOrWhiteSpace(parentSpanId))
            {
                log.AttributesS["parent.span.id"] = parentSpanId;
            }

            if (!string.IsNullOrWhiteSpace(activity.StatusDescription))
            {
                log.AttributesS["span.status.description"] = activity.StatusDescription;
            }

            foreach (var tag in activity.TagObjects)
            {
                LogDBExporterHelpers.AddAttribute(log, $"tag.{tag.Key}", tag.Value);
            }

            for (var index = 0; index < events.Length; index++)
            {
                var evt = events[index];
                log.Label.Add($"event:{evt.Name}");
                log.AttributesS[$"event.{index}.name"] = evt.Name;
                log.AttributesD[$"event.{index}.timestamp"] = evt.Timestamp.UtcDateTime;

                foreach (var eventTag in evt.Tags)
                {
                    LogDBExporterHelpers.AddAttribute(
                        log,
                        $"event.{index}.tag.{eventTag.Key}",
                        eventTag.Value);
                }
            }

            for (var index = 0; index < links.Length; index++)
            {
                log.AttributesS[$"link.{index}.trace.id"] = links[index].Context.TraceId.ToString();
                log.AttributesS[$"link.{index}.span.id"] = links[index].Context.SpanId.ToString();
            }

            return log;
        }

        private LogRelation? ConvertParentRelation(
            Activity activity,
            string serviceName,
            string environment,
            string collection)
        {
            var parentSpanId = GetParentSpanId(activity);
            if (string.IsNullOrWhiteSpace(parentSpanId))
            {
                return null;
            }

            var traceId = activity.TraceId.ToString();
            var childSpanId = activity.SpanId.ToString();

            return new LogRelation
            {
                ApiKey = _options.ApiKey,
                Collection = collection,
                Application = serviceName,
                Environment = environment,
                Origin = $"{traceId}:{parentSpanId}",
                Subject = LogDBExporterHelpers.BuildSpanEntityId(activity.TraceId, activity.SpanId),
                Relation = "PARENT_OF",
                DateIn = activity.StartTimeUtc,
                OriginProperties = new Dictionary<string, object>
                {
                    ["type"] = "span",
                    ["trace.id"] = traceId,
                    ["span.id"] = parentSpanId
                },
                SubjectProperties = new Dictionary<string, object>
                {
                    ["type"] = "span",
                    ["trace.id"] = traceId,
                    ["span.id"] = childSpanId,
                    ["name"] = activity.DisplayName,
                    ["kind"] = activity.Kind.ToString()
                },
                RelationProperties = new Dictionary<string, object>
                {
                    ["trace.id"] = traceId,
                    ["parent.span.id"] = parentSpanId,
                    ["child.span.id"] = childSpanId
                }
            };
        }

        private static string? GetParentSpanId(Activity activity)
        {
            if (activity.ParentSpanId != default)
            {
                return activity.ParentSpanId.ToString();
            }

            if (string.IsNullOrWhiteSpace(activity.ParentId))
            {
                return null;
            }

            // W3C traceparent format: 00-<trace-id>-<span-id>-<flags>
#if NETFRAMEWORK
            var split = activity.ParentId.Split(new[] { '-' }, StringSplitOptions.RemoveEmptyEntries);
#else
            var split = activity.ParentId.Split('-', StringSplitOptions.RemoveEmptyEntries);
#endif
            if (split.Length >= 3 && split[2].Length == 16)
            {
                return split[2];
            }

            return activity.ParentId;
        }
    }
}
