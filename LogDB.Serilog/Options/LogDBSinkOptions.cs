using System;
using Serilog.Events;
using LogDB.Extensions.Logging;

namespace LogDB.Serilog
{
    /// <summary>
    /// Options for configuring the LogDB Serilog sink
    /// </summary>
    public class LogDBSinkOptions
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
        /// Explicit default payload type used when a log event does not contain LogDBType.
        /// Keep null to require LogDBType on every event.
        /// </summary>
        public LogDBPayloadType? DefaultPayloadType { get; set; }

        /// <summary>
        /// Minimum log level to send to LogDB
        /// </summary>
        public LogEventLevel RestrictedToMinimumLevel { get; set; } = LogEventLevel.Information;

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
        /// Format provider for rendering messages
        /// </summary>
        public IFormatProvider? FormatProvider { get; set; }

        /// <summary>
        /// Custom function to filter logs before sending
        /// </summary>
        public Func<LogEvent, bool>? Filter { get; set; }

        /// <summary>
        /// Error callback for handling send failures
        /// </summary>
        public Action<Exception, LogEvent>? OnError { get; set; }

        /// <summary>
        /// Enable debug logging for the LogDB client itself
        /// </summary>
        public bool EnableDebugLogging { get; set; } = false;

        /// <summary>
        /// Request timeout
        /// </summary>
        public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(30);

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
                MinimumLevel = MapLogLevel(RestrictedToMinimumLevel),
                EnableBatching = EnableBatching,
                BatchSize = BatchSize,
                FlushInterval = FlushInterval,
                EnableCompression = EnableCompression,
                MaxRetries = MaxRetries,
                EnableCircuitBreaker = EnableCircuitBreaker,
                EnableDebugLogging = EnableDebugLogging,
                RequestTimeout = RequestTimeout
            };
        }

        private static Microsoft.Extensions.Logging.LogLevel MapLogLevel(LogEventLevel level)
        {
            return level switch
            {
                LogEventLevel.Verbose => Microsoft.Extensions.Logging.LogLevel.Trace,
                LogEventLevel.Debug => Microsoft.Extensions.Logging.LogLevel.Debug,
                LogEventLevel.Information => Microsoft.Extensions.Logging.LogLevel.Information,
                LogEventLevel.Warning => Microsoft.Extensions.Logging.LogLevel.Warning,
                LogEventLevel.Error => Microsoft.Extensions.Logging.LogLevel.Error,
                LogEventLevel.Fatal => Microsoft.Extensions.Logging.LogLevel.Critical,
                _ => Microsoft.Extensions.Logging.LogLevel.Information
            };
        }
    }
}






