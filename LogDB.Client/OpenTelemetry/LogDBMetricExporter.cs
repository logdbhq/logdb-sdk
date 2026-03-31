using System;
using System.Collections.Generic;
using System.Globalization;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using LogDB.Client.Models;
using LogDB.Extensions.Logging;

namespace LogDB.OpenTelemetry
{
    /// <summary>
    /// OpenTelemetry metric exporter for LogDB.
    /// Converts OTel metrics to LogBeat (the stable heartbeat/metric track).
    /// </summary>
    public class LogDBMetricExporter : BaseExporter<global::OpenTelemetry.Metrics.Metric>
    {
        private readonly LogDBExporterOptions _options;
        private readonly ILogDBClient _client;

        public LogDBMetricExporter(LogDBExporterOptions options, ILogDBClient? client = null)
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
                DefaultCollection = options.DefaultCollection ?? "metrics",
                EnableBatching = options.ExportProcessorType == ExportProcessorType.Batch,
                BatchSize = options.BatchExportProcessorOptions?.MaxExportBatchSize ?? 512,
                FlushInterval = TimeSpan.FromMilliseconds(
                    options.BatchExportProcessorOptions?.ScheduledDelayMilliseconds ?? 1000),
                Headers = options.Headers
            };

            return new LogDBClient(
                Microsoft.Extensions.Options.Options.Create(loggerOptions));
        }

        public override ExportResult Export(in Batch<global::OpenTelemetry.Metrics.Metric> batch)
        {
            var identity = _options.ResolveIdentity();
            var serviceName = identity.ServiceName;
            var environment = identity.Environment;
            var collection = LogDBExporterHelpers.ResolveCollection(_options, "metrics");

            try
            {
                var logBeats = new List<LogBeat>();

                foreach (var metric in batch)
                {
                    var convertedBeats = ConvertMetricToLogBeats(metric, serviceName, environment, collection);
                    logBeats.AddRange(convertedBeats);
                }

                if (logBeats.Count > 0)
                {
                    var task = _client.SendLogBeatBatchAsync(logBeats);
                    if (!LogDBExporterHelpers.TryWaitForStatus(task, _options, "metric export", out _))
                    {
                        return ExportResult.Failure;
                    }
                }

                return ExportResult.Success;
            }
            catch (Exception ex)
            {
                _options.ReportError(ex, "metric export");
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
                    "metric exporter shutdown flush");
            }
            catch (Exception ex)
            {
                _options.ReportError(ex, "metric exporter shutdown");
                return false;
            }
        }

        private List<LogBeat> ConvertMetricToLogBeats(
            global::OpenTelemetry.Metrics.Metric metric,
            string serviceName,
            string environment,
            string collection)
        {
            var logBeats = new List<LogBeat>();

            foreach (ref readonly var metricPoint in metric.GetMetricPoints())
            {
                var beat = new LogBeat
                {
                    Guid = Guid.NewGuid().ToString(),
                    ApiKey = _options.ApiKey,
                    Measurement = metric.Name,
                    Timestamp = metricPoint.EndTime.UtcDateTime,
                    Collection = collection,
                    // Tag and Field must be initialized before Application/Environment
                    // because those setters append to the Tag list.
                    Tag = new List<LogMeta>(),
                    Field = new List<LogMeta>(),
                    Application = serviceName,
                    Environment = environment
                };

                foreach (var tag in metricPoint.Tags)
                {
                    LogDBExporterHelpers.AddTag(beat.Tag, tag.Key, tag.Value?.ToString());
                }

                LogDBExporterHelpers.AddTag(beat.Tag, "metric.type", metric.MetricType.ToString());
                LogDBExporterHelpers.AddTag(beat.Tag, "metric.temporality", metric.Temporality.ToString());

                if (!string.IsNullOrEmpty(metric.Unit))
                {
                    LogDBExporterHelpers.AddTag(beat.Tag, "metric.unit", metric.Unit);
                }

                if (!string.IsNullOrEmpty(metric.Description))
                {
                    LogDBExporterHelpers.AddTag(beat.Tag, "metric.description", metric.Description);
                }

                var metricValue = ExtractMetricValue(metric.MetricType, metricPoint);
                beat.Field.Add(new LogMeta
                {
                    Key = "value",
                    Value = metricValue.ToString(CultureInfo.InvariantCulture)
                });

                if (metric.MetricType == MetricType.Histogram || metric.MetricType == MetricType.ExponentialHistogram)
                {
                    try
                    {
                        beat.Field.Add(new LogMeta
                        {
                            Key = "count",
                            Value = metricPoint.GetHistogramCount().ToString(CultureInfo.InvariantCulture)
                        });

                        beat.Field.Add(new LogMeta
                        {
                            Key = "sum",
                            Value = metricPoint.GetHistogramSum().ToString(CultureInfo.InvariantCulture)
                        });
                    }
                    catch
                    {
                        _options.WriteDebug($"Histogram fields are not available for metric '{metric.Name}'.");
                    }
                }

                logBeats.Add(beat);
            }

            return logBeats;
        }

        private static double ExtractMetricValue(MetricType metricType, in MetricPoint metricPoint)
        {
            try
            {
                return metricType switch
                {
                    MetricType.LongSum or MetricType.LongSumNonMonotonic => metricPoint.GetSumLong(),
                    MetricType.DoubleSum or MetricType.DoubleSumNonMonotonic => metricPoint.GetSumDouble(),
                    MetricType.LongGauge => metricPoint.GetGaugeLastValueLong(),
                    MetricType.DoubleGauge => metricPoint.GetGaugeLastValueDouble(),
                    MetricType.Histogram or MetricType.ExponentialHistogram => metricPoint.GetHistogramSum(),
                    _ => 0d
                };
            }
            catch
            {
                // Best-effort fallback for mixed/unknown aggregation implementations.
                try { return metricPoint.GetSumDouble(); } catch { }
                try { return metricPoint.GetSumLong(); } catch { }
                try { return metricPoint.GetGaugeLastValueDouble(); } catch { }
                try { return metricPoint.GetGaugeLastValueLong(); } catch { }
                try { return metricPoint.GetHistogramSum(); } catch { }
                return 0d;
            }
        }
    }
}
