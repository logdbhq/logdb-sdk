using System;
using NLog;
using LogDB.Extensions.Logging;

namespace LogDB.NLog
{
    /// <summary>
    /// Options for configuring the LogDB NLog target
    /// </summary>
    public class LogDBTargetOptions
    {
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
        /// Maximum number of retries for failed requests
        /// </summary>
        public int MaxRetries { get; set; } = 3;

        /// <summary>
        /// Enable circuit breaker pattern
        /// </summary>
        public bool EnableCircuitBreaker { get; set; } = true;

        /// <summary>
        /// Custom function to filter logs before sending
        /// </summary>
        public Func<LogEventInfo, bool>? Filter { get; set; }

        /// <summary>
        /// Error callback for handling send failures
        /// </summary>
        public Action<Exception, LogEventInfo>? OnError { get; set; }

        /// <summary>
        /// Enable debug logging for the LogDB client itself
        /// </summary>
        public bool EnableDebugLogging { get; set; } = false;

        /// <summary>
        /// Request timeout
        /// </summary>
        public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Retry delay
        /// </summary>
        public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);

        /// <summary>
        /// Retry backoff multiplier
        /// </summary>
        public double RetryBackoffMultiplier { get; set; } = 2.0;

        /// <summary>
        /// Circuit breaker failure threshold (0.0 to 1.0)
        /// </summary>
        public double CircuitBreakerFailureThreshold { get; set; } = 0.5;

        /// <summary>
        /// Circuit breaker duration of break
        /// </summary>
        public TimeSpan CircuitBreakerDurationOfBreak { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Fallback target to use if LogDB fails
        /// </summary>
        public global::NLog.Targets.Target? FallbackTarget { get; set; }

        /// <summary>
        /// Convert to LogDBLoggerOptions for use with LogDBClient
        /// </summary>
        internal LogDBLoggerOptions ToLogDBLoggerOptions()
        {
            return new LogDBLoggerOptions
            {
                ApiKey = ApiKey,
                ServiceUrl = ServiceUrl,
                Protocol = Protocol,
                DefaultCollection = DefaultCollection,
                DefaultApplication = DefaultApplication,
                DefaultEnvironment = DefaultEnvironment,
                MinimumLevel = MapLogLevel(MinimumLevel),
                EnableBatching = EnableBatching,
                BatchSize = BatchSize,
                FlushInterval = FlushInterval,
                EnableCompression = EnableCompression,
                MaxRetries = MaxRetries,
                EnableCircuitBreaker = EnableCircuitBreaker,
                EnableDebugLogging = EnableDebugLogging,
                RequestTimeout = RequestTimeout,
                RetryDelay = RetryDelay,
                RetryBackoffMultiplier = RetryBackoffMultiplier,
                CircuitBreakerFailureThreshold = CircuitBreakerFailureThreshold,
                CircuitBreakerDurationOfBreak = CircuitBreakerDurationOfBreak
            };
        }

        private static Microsoft.Extensions.Logging.LogLevel MapLogLevel(global::NLog.LogLevel level)
        {
            // NLog.LogLevel is a class not an enum, so we need to use ordinal comparisons
            if (level.Ordinal <= global::NLog.LogLevel.Trace.Ordinal)
                return Microsoft.Extensions.Logging.LogLevel.Trace;
            if (level.Ordinal <= global::NLog.LogLevel.Debug.Ordinal)
                return Microsoft.Extensions.Logging.LogLevel.Debug;
            if (level.Ordinal <= global::NLog.LogLevel.Info.Ordinal)
                return Microsoft.Extensions.Logging.LogLevel.Information;
            if (level.Ordinal <= global::NLog.LogLevel.Warn.Ordinal)
                return Microsoft.Extensions.Logging.LogLevel.Warning;
            if (level.Ordinal <= global::NLog.LogLevel.Error.Ordinal)
                return Microsoft.Extensions.Logging.LogLevel.Error;

            return Microsoft.Extensions.Logging.LogLevel.Critical;
        }
    }
}






