using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Configuration;
using Microsoft.Extensions.Options;

namespace LogDB.Extensions.Logging
{
    /// <summary>
    /// Extension methods for adding LogDB logger
    /// </summary>
    public static class LogDBLoggerExtensions
    {
        /// <summary>
        /// Adds LogDB logger to the logging builder
        /// </summary>
        public static ILoggingBuilder AddLogDB(this ILoggingBuilder builder)
        {
            builder.AddConfiguration();
            
            builder.Services.TryAddEnumerable(
                ServiceDescriptor.Singleton<ILoggerProvider, LogDBLoggerProvider>());
            
            // Register LogDBClient without ILogger to avoid circular dependency
            // When LogDBClient is used as part of the logging infrastructure,
            // it shouldn't log to itself
            builder.Services.TryAddSingleton<ILogDBClient>(sp =>
            {
                var options = sp.GetRequiredService<IOptions<LogDBLoggerOptions>>();
                return new LogDBClient(options, logger: null);
            });
            
            LoggerProviderOptions.RegisterProviderOptions<LogDBLoggerOptions, LogDBLoggerProvider>(builder.Services);
            
            return builder;
        }

        /// <summary>
        /// Adds LogDB logger to the logging builder with configuration
        /// </summary>
        public static ILoggingBuilder AddLogDB(this ILoggingBuilder builder, Action<LogDBLoggerOptions> configure)
        {
            builder.AddLogDB();
            builder.Services.Configure(configure);
            return builder;
        }

        /// <summary>
        /// Adds LogDB logger to the logging builder with named options
        /// </summary>
        public static ILoggingBuilder AddLogDB(this ILoggingBuilder builder, string name, Action<LogDBLoggerOptions> configure)
        {
            builder.AddLogDB();
            builder.Services.Configure(name, configure);
            return builder;
        }

        /// <summary>
        /// Add a log enricher
        /// </summary>
        public static LogDBLoggerOptions AddEnricher(this LogDBLoggerOptions options, ILogEnricher enricher)
        {
            options.Enrichers.Add(enricher);
            return options;
        }

        /// <summary>
        /// Add a log enricher using a delegate
        /// </summary>
        public static LogDBLoggerOptions AddEnricher(this LogDBLoggerOptions options, Action<LogDB.Client.Models.Log> enrichAction)
        {
            options.Enrichers.Add(new DelegateEnricher(enrichAction));
            return options;
        }

        /// <summary>
        /// Configure LogDB with environment variables
        /// </summary>
        public static LogDBLoggerOptions ConfigureFromEnvironment(this LogDBLoggerOptions options)
        {
            var apiKey = Environment.GetEnvironmentVariable("LOGDB_API_KEY");
            if (!string.IsNullOrEmpty(apiKey))
                options.ApiKey = apiKey;

            var serviceUrl = Environment.GetEnvironmentVariable("LOGDB_SERVICE_URL");
            if (!string.IsNullOrEmpty(serviceUrl))
                options.ServiceUrl = serviceUrl;

            var collection = Environment.GetEnvironmentVariable("LOGDB_DEFAULT_COLLECTION");
            if (!string.IsNullOrEmpty(collection))
                options.DefaultCollection = collection;

            var environment = Environment.GetEnvironmentVariable("LOGDB_ENVIRONMENT");
            if (!string.IsNullOrEmpty(environment))
                options.DefaultEnvironment = environment;

            var batchingStr = Environment.GetEnvironmentVariable("LOGDB_ENABLE_BATCHING");
            if (bool.TryParse(batchingStr, out var batching))
                options.EnableBatching = batching;

            var protocolStr = Environment.GetEnvironmentVariable("LOGDB_PROTOCOL");
            if (Enum.TryParse<LogDBProtocol>(protocolStr, true, out var protocol))
                options.Protocol = protocol;

            return options;
        }

        private class DelegateEnricher : ILogEnricher
        {
            private readonly Action<LogDB.Client.Models.Log> _enrichAction;

            public DelegateEnricher(Action<LogDB.Client.Models.Log> enrichAction)
            {
                _enrichAction = enrichAction;
            }

            public void Enrich(LogDB.Client.Models.Log log)
            {
                _enrichAction(log);
            }
        }
    }
}

