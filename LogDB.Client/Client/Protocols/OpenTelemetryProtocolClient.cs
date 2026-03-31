using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using LogDB.Client.Models;
using LogDB.Client.Services;
using Microsoft.Extensions.Logging;

namespace LogDB.Extensions.Logging
{
    /// <summary>
    /// OpenTelemetry protocol client implementation
    /// </summary>
    internal class OpenTelemetryProtocolClient : IProtocolClient, IDisposable
    {
        private readonly LogDBLoggerOptions _options;
        private readonly ILogger? _logger;
        private readonly HttpClient _httpClient;
        private bool _disposed;

        public OpenTelemetryProtocolClient(LogDBLoggerOptions options, ILogger? logger)
        {
            _options = options;
            _logger = logger;

            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(GetOtlpEndpoint()),
                Timeout = _options.RequestTimeout
            };

            // Add headers
            foreach (var header in _options.Headers)
            {
                _httpClient.DefaultRequestHeaders.Add(header.Key, header.Value);
            }

            // Add API key header if not already present
            if (!_options.Headers.Keys.Any(k => k.Equals("X-LogDB-ApiKey", StringComparison.OrdinalIgnoreCase)) &&
                !string.IsNullOrWhiteSpace(_options.ApiKey))
            {
                _httpClient.DefaultRequestHeaders.Add("X-LogDB-ApiKey", _options.ApiKey);
            }
        }

        private string GetOtlpEndpoint()
        {
            if (!string.IsNullOrEmpty(_options.ServiceUrl))
            {
                return _options.ServiceUrl;
            }

            // Check for OTEL standard environment variable
            var otelEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
            if (!string.IsNullOrEmpty(otelEndpoint))
            {
                return otelEndpoint;
            }

            // Try discovery service
            try
            {
                var discoveryService = new DiscoveryService();
                var url = discoveryService.DiscoverServiceUrl("otlp-endpoint", _options.ApiKey);
                if (!string.IsNullOrEmpty(url))
                {
                    return url;
                }
            }
            catch
            {
                // Discovery failed, fall through to error
            }

            throw new InvalidOperationException(
                "Unable to determine OTLP endpoint. " +
                "Please set ServiceUrl in options or configure OTEL_EXPORTER_OTLP_ENDPOINT environment variable.");
        }

        public async Task<LogResponseStatus> SendLogAsync(Log log, CancellationToken cancellationToken = default)
        {
            var request = CreateOtlpLogsRequest(new[] { log });
            return await SendOtlpRequest("/v1/logs", request, cancellationToken);
        }

        public async Task<LogResponseStatus> SendLogPointAsync(LogPoint logPoint, CancellationToken cancellationToken = default)
        {
            var request = CreateOtlpMetricsRequest(new[] { logPoint });
            return await SendOtlpRequest("/v1/metrics", request, cancellationToken);
        }

        public async Task<LogResponseStatus> SendLogBeatAsync(LogBeat logBeat, CancellationToken cancellationToken = default)
        {
            // LogBeat is similar to LogPoint, send as metric
            var request = CreateOtlpMetricsRequestFromBeat(new[] { logBeat });
            return await SendOtlpRequest("/v1/metrics", request, cancellationToken);
        }

        public async Task<LogResponseStatus> SendLogCacheAsync(LogCache logCache, CancellationToken cancellationToken = default)
        {
            // LogCache can be sent as a log with special attributes
            var log = ConvertLogCacheToLog(logCache);
            return await SendLogAsync(log, cancellationToken);
        }

        public async Task<LogResponseStatus> SendLogRelationAsync(LogRelation logRelation, CancellationToken cancellationToken = default)
        {
            // LogRelation can be sent as a trace span
            var request = CreateOtlpTracesRequest(new[] { logRelation });
            return await SendOtlpRequest("/v1/traces", request, cancellationToken);
        }

        // Batch methods
        public async Task<LogResponseStatus> SendLogBatchAsync(IReadOnlyList<Log> logs, CancellationToken cancellationToken = default)
        {
            var request = CreateOtlpLogsRequest(logs);
            return await SendOtlpRequest("/v1/logs", request, cancellationToken);
        }

        public async Task<LogResponseStatus> SendLogPointBatchAsync(IReadOnlyList<LogPoint> logPoints, CancellationToken cancellationToken = default)
        {
            var request = CreateOtlpMetricsRequest(logPoints);
            return await SendOtlpRequest("/v1/metrics", request, cancellationToken);
        }

        public async Task<LogResponseStatus> SendLogBeatBatchAsync(IReadOnlyList<LogBeat> logBeats, CancellationToken cancellationToken = default)
        {
            var request = CreateOtlpMetricsRequestFromBeat(logBeats);
            return await SendOtlpRequest("/v1/metrics", request, cancellationToken);
        }

        public async Task<LogResponseStatus> SendLogCacheBatchAsync(IReadOnlyList<LogCache> logCaches, CancellationToken cancellationToken = default)
        {
            var logs = logCaches.Select(ConvertLogCacheToLog).ToList();
            return await SendLogBatchAsync(logs, cancellationToken);
        }

        public async Task<LogResponseStatus> SendLogRelationBatchAsync(IReadOnlyList<LogRelation> logRelations, CancellationToken cancellationToken = default)
        {
            var request = CreateOtlpTracesRequest(logRelations);
            return await SendOtlpRequest("/v1/traces", request, cancellationToken);
        }

        private async Task<LogResponseStatus> SendOtlpRequest<T>(string endpoint, T request, CancellationToken cancellationToken)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync(endpoint, request, cancellationToken);
                
                if (response.IsSuccessStatusCode)
                {
                    return LogResponseStatus.Success;
                }

                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    return LogResponseStatus.NotAuthorized;
                }

                _logger?.LogWarning("OTLP request failed with status {StatusCode}", response.StatusCode);
                return LogResponseStatus.Failed;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to send OTLP request");
                return LogResponseStatus.Failed;
            }
        }

        private OtlpLogsRequest CreateOtlpLogsRequest(IReadOnlyList<Log> logs)
        {
            var resourceLogs = new ResourceLogs
            {
                Resource = CreateResource(),
                ScopeLogs = new List<ScopeLogs>
                {
                    new ScopeLogs
                    {
                        Scope = new InstrumentationScope
                        {
                            Name = "LogDB.Client",
                            Version = "1.0.0"
                        },
                        LogRecords = logs.Select(ConvertToOtlpLog).ToList()
                    }
                }
            };

            return new OtlpLogsRequest
            {
                ResourceLogs = new List<ResourceLogs> { resourceLogs }
            };
        }

        private LogRecord ConvertToOtlpLog(Log log)
        {
            var record = new LogRecord
            {
                TimeUnixNano = ((ulong)new DateTimeOffset(log.Timestamp).ToUnixTimeMilliseconds()) * 1_000_000,
                ObservedTimeUnixNano = ((ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()) * 1_000_000,
                SeverityNumber = MapLogLevel(log.Level),
                SeverityText = log.Level.ToString(),
                Body = new AnyValue { StringValue = log.Message },
                TraceId = log.CorrelationId,
                Attributes = new List<KeyValue>()
            };

            // Add standard attributes
            if (!string.IsNullOrEmpty(log.Application))
                record.Attributes.Add(CreateKeyValue("application", log.Application));
            if (!string.IsNullOrEmpty(log.Environment))
                record.Attributes.Add(CreateKeyValue("environment", log.Environment));
            if (!string.IsNullOrEmpty(log.Source))
                record.Attributes.Add(CreateKeyValue("source", log.Source));
            if (!string.IsNullOrEmpty(log.Exception))
                record.Attributes.Add(CreateKeyValue("exception.type", log.Exception));
            if (!string.IsNullOrEmpty(log.StackTrace))
                record.Attributes.Add(CreateKeyValue("exception.stacktrace", log.StackTrace));

            // Add custom attributes
            foreach (var attr in log.AttributesS)
                record.Attributes.Add(CreateKeyValue(attr.Key, attr.Value));
            foreach (var attr in log.AttributesN)
                record.Attributes.Add(CreateKeyValue(attr.Key, attr.Value));
            foreach (var attr in log.AttributesB)
                record.Attributes.Add(CreateKeyValue(attr.Key, attr.Value));
            foreach (var attr in log.AttributesD)
                record.Attributes.Add(CreateKeyValue(attr.Key, attr.Value.ToString("O")));

            // Add labels as attributes
            foreach (var label in log.Label)
                record.Attributes.Add(CreateKeyValue($"label.{label}", true));

            return record;
        }

        private OtlpMetricsRequest CreateOtlpMetricsRequest(IReadOnlyList<LogPoint> logPoints)
        {
            var metrics = logPoints
                .GroupBy(lp => lp.Measurement)
                .Select(group => CreateMetric(group.Key, group.ToList()))
                .ToList();

            var resourceMetrics = new ResourceMetrics
            {
                Resource = CreateResource(),
                ScopeMetrics = new List<ScopeMetrics>
                {
                    new ScopeMetrics
                    {
                        Scope = new InstrumentationScope
                        {
                            Name = "LogDB.Client",
                            Version = "1.0.0"
                        },
                        Metrics = metrics
                    }
                }
            };

            return new OtlpMetricsRequest
            {
                ResourceMetrics = new List<ResourceMetrics> { resourceMetrics }
            };
        }

        private OtlpMetricsRequest CreateOtlpMetricsRequestFromBeat(IReadOnlyList<LogBeat> logBeats)
        {
            var logPoints = logBeats.Select(lb => new LogPoint
            {
                ApiKey = lb.ApiKey,
                Collection = lb.Collection,
                Guid = lb.Guid,
                Measurement = lb.Measurement,
                Tag = lb.Tag.Select(t => new LogMeta { Key = t.Key, Value = t.Value }).ToList(),
                Field = lb.Field.Select(f => new LogMeta { Key = f.Key, Value = f.Value }).ToList(),
                Timestamp = lb.Timestamp
            }).ToList();

            return CreateOtlpMetricsRequest(logPoints);
        }

        private Metric CreateMetric(string name, List<LogPoint> points)
        {
            var dataPoints = points.Select(p => new NumberDataPoint
            {
                TimeUnixNano = ((ulong)new DateTimeOffset(p.Timestamp).ToUnixTimeMilliseconds()) * 1_000_000,
                AsDouble = GetFirstNumericValue(p.Field),
                Attributes = ConvertTagsToAttributes(p.Tag)
            }).ToList();

            return new Metric
            {
                Name = name,
                Gauge = new Gauge
                {
                    DataPoints = dataPoints
                }
            };
        }

        private double GetFirstNumericValue(List<LogMeta> fields)
        {
            var firstField = fields.FirstOrDefault();
            if (firstField != null && double.TryParse(firstField.Value, out var value))
            {
                return value;
            }
            return 0.0;
        }

        private List<KeyValue> ConvertTagsToAttributes(List<LogMeta> tags)
        {
            return tags.Select(t => CreateKeyValue(t.Key, t.Value)).ToList();
        }

        private OtlpTracesRequest CreateOtlpTracesRequest(IReadOnlyList<LogRelation> relations)
        {
            var spans = relations.Select(ConvertToOtlpSpan).ToList();

            var resourceSpans = new ResourceSpans
            {
                Resource = CreateResource(),
                ScopeSpans = new List<ScopeSpans>
                {
                    new ScopeSpans
                    {
                        Scope = new InstrumentationScope
                        {
                            Name = "LogDB.Client",
                            Version = "1.0.0"
                        },
                        Spans = spans
                    }
                }
            };

            return new OtlpTracesRequest
            {
                ResourceSpans = new List<ResourceSpans> { resourceSpans }
            };
        }

        private OtlpSpan ConvertToOtlpSpan(LogRelation relation)
        {
            var span = new OtlpSpan
            {
                TraceId = Guid.NewGuid().ToString("N"),
                SpanId = Guid.NewGuid().ToString("N").Substring(0, 16),
                Name = $"{relation.Origin} {relation.Relation} {relation.Subject}",
                StartTimeUnixNano = ((ulong)new DateTimeOffset(relation.DateIn ?? DateTime.UtcNow).ToUnixTimeMilliseconds()) * 1_000_000,
                EndTimeUnixNano = ((ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()) * 1_000_000,
                Kind = 1, // SPAN_KIND_INTERNAL
                Attributes = new List<KeyValue>
                {
                    CreateKeyValue("relation.origin", relation.Origin),
                    CreateKeyValue("relation.type", relation.Relation),
                    CreateKeyValue("relation.subject", relation.Subject)
                }
            };

            // Add origin properties
            if (relation.OriginProperties != null)
            {
                foreach (var prop in relation.OriginProperties)
                {
                    span.Attributes.Add(CreateKeyValue($"origin.{prop.Key}", prop.Value?.ToString() ?? ""));
                }
            }

            // Add subject properties
            if (relation.SubjectProperties != null)
            {
                foreach (var prop in relation.SubjectProperties)
                {
                    span.Attributes.Add(CreateKeyValue($"subject.{prop.Key}", prop.Value?.ToString() ?? ""));
                }
            }

            // Add relation properties
            if (relation.RelationProperties != null)
            {
                foreach (var prop in relation.RelationProperties)
                {
                    span.Attributes.Add(CreateKeyValue($"relation.{prop.Key}", prop.Value?.ToString() ?? ""));
                }
            }

            return span;
        }

        private Log ConvertLogCacheToLog(LogCache logCache)
        {
            return new Log
            {
                Guid = logCache.Guid,
                ApiKey = logCache.ApiKey,
                Timestamp = DateTime.UtcNow,
                Message = $"Cache entry: {logCache.Key}",
                Level = LogDB.Client.Models.LogLevel.Info,
                Collection = "cache",
                AttributesS = new Dictionary<string, string>
                {
                    ["cache.key"] = logCache.Key,
                    ["cache.value"] = logCache.Value,
                    ["type"] = "LogCache"
                }
            };
        }

        private Resource CreateResource()
        {
            return new Resource
            {
                Attributes = new List<KeyValue>
                {
                    CreateKeyValue("service.name", _options.DefaultApplication ?? "LogDB.Client"),
                    CreateKeyValue("deployment.environment", _options.DefaultEnvironment),
                    CreateKeyValue("logdb.collection", _options.DefaultCollection)
                }
            };
        }

        private KeyValue CreateKeyValue(string key, string value)
        {
            return new KeyValue
            {
                Key = key,
                Value = new AnyValue { StringValue = value }
            };
        }

        private KeyValue CreateKeyValue(string key, double value)
        {
            return new KeyValue
            {
                Key = key,
                Value = new AnyValue { DoubleValue = value }
            };
        }

        private KeyValue CreateKeyValue(string key, bool value)
        {
            return new KeyValue
            {
                Key = key,
                Value = new AnyValue { BoolValue = value }
            };
        }

        private int MapLogLevel(LogDB.Client.Models.LogLevel level)
        {
            return level switch
            {
                LogDB.Client.Models.LogLevel.Debug => 5,
                LogDB.Client.Models.LogLevel.Info => 9,
                LogDB.Client.Models.LogLevel.Warning => 13,
                LogDB.Client.Models.LogLevel.Error => 17,
                LogDB.Client.Models.LogLevel.Critical => 21,
                _ => 9
            };
        }

        public void Dispose()
        {
            if (_disposed) return;

            _httpClient?.Dispose();
            _disposed = true;
        }
    }
}
