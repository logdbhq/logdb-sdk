using System;
using Serilog.Configuration;
using Serilog.Events;
using LogDB.Serilog;

namespace Serilog
{
    /// <summary>
    /// Extension methods for configuring LogDB sink in Serilog
    /// </summary>
    public static class LogDBSinkExtensions
    {
        /// <summary>
        /// Write log events to LogDB
        /// </summary>
        /// <param name="sinkConfiguration">The logger sink configuration</param>
        /// <param name="configureOptions">Action to configure the LogDB sink options</param>
        /// <returns>Logger configuration for chaining</returns>
        public static LoggerConfiguration LogDB(
            this LoggerSinkConfiguration sinkConfiguration,
            Action<LogDBSinkOptions> configureOptions)
        {
            if (sinkConfiguration == null)
                throw new ArgumentNullException(nameof(sinkConfiguration));
            if (configureOptions == null)
                throw new ArgumentNullException(nameof(configureOptions));

            var options = new LogDBSinkOptions();
            configureOptions(options);

            return sinkConfiguration.Sink(new LogDBSink(options));
        }

        /// <summary>
        /// Write log events to LogDB with default options
        /// </summary>
        /// <param name="sinkConfiguration">The logger sink configuration</param>
        /// <param name="apiKey">LogDB API key</param>
        /// <param name="defaultPayloadType">Explicit default payload type for events that do not set LogDBType</param>
        /// <param name="restrictedToMinimumLevel">Minimum log level</param>
        /// <returns>Logger configuration for chaining</returns>
        public static LoggerConfiguration LogDB(
            this LoggerSinkConfiguration sinkConfiguration,
            string apiKey,
            LogDBPayloadType defaultPayloadType,
            LogEventLevel restrictedToMinimumLevel = LogEventLevel.Information)
        {
            if (sinkConfiguration == null)
                throw new ArgumentNullException(nameof(sinkConfiguration));
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new ArgumentException("API key is required", nameof(apiKey));

            return sinkConfiguration.LogDB(options =>
            {
                options.ApiKey = apiKey;
                options.DefaultPayloadType = defaultPayloadType;
                options.RestrictedToMinimumLevel = restrictedToMinimumLevel;
            });
        }
    }
}






