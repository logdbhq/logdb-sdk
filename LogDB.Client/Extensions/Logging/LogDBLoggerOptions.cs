using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using LogDB.Client.Models;

namespace LogDB.Extensions.Logging
{
    /// <summary>
    /// Options for configuring LogDB logger
    /// </summary>
    public class LogDBLoggerOptions
    {
        /// <summary>
        /// LogDB API key for authentication
        /// </summary>
        public string ApiKey { get; set; } = string.Empty;

        /// <summary>
        /// Account ID resolved from API key. Manual assignment is intentionally blocked.
        /// </summary>
        public int? AccountId { get; private set; }

        /// <summary>
        /// Service URL for LogDB Writer. If not specified, auto-discovery will be used
        /// </summary>
        public string? ServiceUrl { get; set; }

        /// <summary>
        /// Service URL for LogDB Reader. If not specified, auto-discovery will be used
        /// </summary>
        public string? ReaderServiceUrl { get; set; }

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
        /// Minimum log level
        /// </summary>
        public Microsoft.Extensions.Logging.LogLevel MinimumLevel { get; set; } = Microsoft.Extensions.Logging.LogLevel.Information;

        /// <summary>
        /// Include scopes in log data
        /// </summary>
        public bool IncludeScopes { get; set; } = true;

        /// <summary>
        /// Enable batching of log entries
        /// </summary>
        public bool EnableBatching { get; set; } = true;

        /// <summary>
        /// Number of log entries to batch before sending
        /// </summary>
        public int BatchSize { get; set; } = 100;

        /// <summary>
        /// Maximum time to wait before flushing a batch
        /// </summary>
        public TimeSpan FlushInterval { get; set; } = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Enable compression for payloads
        /// </summary>
        public bool EnableCompression { get; set; } = true;

        /// <summary>
        /// Maximum number of concurrent batches to process
        /// </summary>
        public int MaxDegreeOfParallelism { get; set; } = 4;

        /// <summary>
        /// Maximum number of retries for failed requests
        /// </summary>
        public int MaxRetries { get; set; } = 3;

        /// <summary>
        /// Maximum number of times a failed batch entry is re-queued before falling back to individual send.
        /// Default is 2 (entries get at most 2 additional batch attempts before individual fallback).
        /// </summary>
        public int MaxBatchRetries { get; set; } = 2;

        /// <summary>
        /// Initial retry delay
        /// </summary>
        public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);

        /// <summary>
        /// Retry backoff multiplier
        /// </summary>
        public double RetryBackoffMultiplier { get; set; } = 2.0;

        /// <summary>
        /// Enable circuit breaker pattern
        /// </summary>
        public bool EnableCircuitBreaker { get; set; } = true;

        /// <summary>
        /// Circuit breaker failure threshold (0-1)
        /// </summary>
        public double CircuitBreakerFailureThreshold { get; set; } = 0.5;

        /// <summary>
        /// Circuit breaker sampling duration
        /// </summary>
        public TimeSpan CircuitBreakerSamplingDuration { get; set; } = TimeSpan.FromSeconds(10);

        /// <summary>
        /// Circuit breaker break duration
        /// </summary>
        public TimeSpan CircuitBreakerDurationOfBreak { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Enable local buffering for offline scenarios
        /// </summary>
        public bool EnableLocalBuffer { get; set; } = false;

        /// <summary>
        /// Maximum number of log entries to buffer locally
        /// </summary>
        public int LocalBufferSize { get; set; } = 10000;

        /// <summary>
        /// Path for local buffer storage
        /// </summary>
        public string? LocalBufferPath { get; set; }

        /// <summary>
        /// Enable sampling
        /// </summary>
        public bool EnableSampling { get; set; } = false;

        /// <summary>
        /// Sampling rate (0-1, where 1 = 100%)
        /// </summary>
        public double SamplingRate { get; set; } = 1.0;

        /// <summary>
        /// Always include error logs regardless of sampling
        /// </summary>
        public bool AlwaysIncludeErrors { get; set; } = true;

        /// <summary>
        /// Custom filter function
        /// </summary>
        public Func<string, Microsoft.Extensions.Logging.LogLevel, bool>? Filter { get; set; }

        /// <summary>
        /// Log enrichers
        /// </summary>
        public List<ILogEnricher> Enrichers { get; set; } = new();

        /// <summary>
        /// Error callback
        /// </summary>
        public Action<Exception, IReadOnlyList<Log>>? OnError { get; set; }

        /// <summary>
        /// Enable debug logging for the LogDB client itself
        /// </summary>
        public bool EnableDebugLogging { get; set; } = false;

        /// <summary>
        /// Additional headers to send with requests
        /// </summary>
        public Dictionary<string, string> Headers { get; set; } = new();

        /// <summary>
        /// Request timeout
        /// </summary>
        public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// DANGEROUS: Skip SSL certificate validation. Only use for local development with self-signed certificates.
        /// Never enable this in production as it makes connections vulnerable to man-in-the-middle attacks.
        /// </summary>
        public bool DangerouslyAcceptAnyServerCertificate { get; set; } = false;
    }

    /// <summary>
    /// Protocol options for LogDB communication
    /// </summary>
    public enum LogDBProtocol
    {
        /// <summary>
        /// Native LogDB gRPC protocol (default)
        /// </summary>
        Native,

        /// <summary>
        /// OpenTelemetry protocol
        /// </summary>
        OpenTelemetry,

        /// <summary>
        /// REST API fallback
        /// </summary>
        Rest
    }

    /// <summary>
    /// Interface for log enrichers
    /// </summary>
    public interface ILogEnricher
    {
        /// <summary>
        /// Enrich a log entry with additional data
        /// </summary>
        void Enrich(Log log);
    }
}
