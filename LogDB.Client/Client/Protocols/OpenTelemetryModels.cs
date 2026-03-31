namespace LogDB.Extensions.Logging
{
    // Simplified OTLP models for HTTP/JSON protocol
    // These are minimal representations for the client to work with

    internal class OtlpLogsRequest
    {
        public List<ResourceLogs>? ResourceLogs { get; set; }
    }

    internal class ResourceLogs
    {
        public Resource? Resource { get; set; }
        public List<ScopeLogs>? ScopeLogs { get; set; }
    }

    internal class Resource
    {
        public List<KeyValue>? Attributes { get; set; }
    }

    internal class ScopeLogs
    {
        public InstrumentationScope? Scope { get; set; }
        public List<LogRecord>? LogRecords { get; set; }
    }

    internal class InstrumentationScope
    {
        public string? Name { get; set; }
        public string? Version { get; set; }
    }

    internal class LogRecord
    {
        public ulong TimeUnixNano { get; set; }
        public ulong ObservedTimeUnixNano { get; set; }
        public int? SeverityNumber { get; set; }
        public string? SeverityText { get; set; }
        public AnyValue? Body { get; set; }
        public List<KeyValue>? Attributes { get; set; }
        public string? TraceId { get; set; }
        public string? SpanId { get; set; }
    }

    internal class KeyValue
    {
        public string? Key { get; set; }
        public AnyValue? Value { get; set; }
    }

    internal class AnyValue
    {
        public string? StringValue { get; set; }
        public bool? BoolValue { get; set; }
        public long? IntValue { get; set; }
        public double? DoubleValue { get; set; }
    }

    internal class OtlpMetricsRequest
    {
        public List<ResourceMetrics>? ResourceMetrics { get; set; }
    }

    internal class ResourceMetrics
    {
        public Resource? Resource { get; set; }
        public List<ScopeMetrics>? ScopeMetrics { get; set; }
    }

    internal class ScopeMetrics
    {
        public InstrumentationScope? Scope { get; set; }
        public List<Metric>? Metrics { get; set; }
    }

    internal class Metric
    {
        public string? Name { get; set; }
        public string? Unit { get; set; }
        public Gauge? Gauge { get; set; }
        public Sum? Sum { get; set; }
        public Histogram? Histogram { get; set; }
    }

    internal class Gauge
    {
        public List<NumberDataPoint>? DataPoints { get; set; }
    }

    internal class Sum
    {
        public List<NumberDataPoint>? DataPoints { get; set; }
    }

    internal class Histogram
    {
        public List<HistogramDataPoint>? DataPoints { get; set; }
    }

    internal class NumberDataPoint
    {
        public ulong TimeUnixNano { get; set; }
        public double? AsDouble { get; set; }
        public long? AsInt { get; set; }
        public List<KeyValue>? Attributes { get; set; }
    }

    internal class HistogramDataPoint
    {
        public ulong TimeUnixNano { get; set; }
        public ulong Count { get; set; }
        public double Sum { get; set; }
        public double? Min { get; set; }
        public double? Max { get; set; }
        public List<KeyValue>? Attributes { get; set; }
    }

    internal class OtlpTracesRequest
    {
        public List<ResourceSpans>? ResourceSpans { get; set; }
    }

    internal class ResourceSpans
    {
        public Resource? Resource { get; set; }
        public List<ScopeSpans>? ScopeSpans { get; set; }
    }

    internal class ScopeSpans
    {
        public InstrumentationScope? Scope { get; set; }
        public List<OtlpSpan>? Spans { get; set; }
    }

    internal class OtlpSpan
    {
        public string? TraceId { get; set; }
        public string? SpanId { get; set; }
        public string? ParentSpanId { get; set; }
        public string? Name { get; set; }
        public int Kind { get; set; }
        public ulong StartTimeUnixNano { get; set; }
        public ulong EndTimeUnixNano { get; set; }
        public List<KeyValue>? Attributes { get; set; }
        public Status? Status { get; set; }
    }

    internal class Status
    {
        public string? Code { get; set; }
        public string? Message { get; set; }
    }
}
