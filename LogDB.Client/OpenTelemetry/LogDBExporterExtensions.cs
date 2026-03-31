using System;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;

namespace LogDB.OpenTelemetry
{
    /// <summary>
    /// Extension methods for adding LogDB exporter to OpenTelemetry
    /// </summary>
    public static class LogDBExporterExtensions
    {
        /// <summary>
        /// Adds LogDB exporter to the TracerProviderBuilder
        /// </summary>
        public static TracerProviderBuilder AddLogDBExporter(
            this TracerProviderBuilder builder,
            Action<LogDBExporterOptions>? configure = null)
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));

            var options = BuildAndValidateOptions(configure);

            builder.ConfigureResource(resourceBuilder =>
            {
                options.Resource = resourceBuilder.Build();
                options.WriteDebug("Captured tracing resource from TracerProviderBuilder.");
            });

            var exporter = new LogDBTraceExporter(options);
            if (options.ExportProcessorType == ExportProcessorType.Simple)
            {
                return builder.AddProcessor(new SimpleActivityExportProcessor(exporter));
            }

            return builder.AddProcessor(new BatchActivityExportProcessor(
                exporter,
                options.BatchExportProcessorOptions?.MaxQueueSize ?? 2048,
                options.BatchExportProcessorOptions?.ScheduledDelayMilliseconds ?? 5000,
                options.BatchExportProcessorOptions?.ExporterTimeoutMilliseconds ?? 30000,
                options.BatchExportProcessorOptions?.MaxExportBatchSize ?? 512));
        }

        /// <summary>
        /// Adds LogDB exporter to the TracerProviderBuilder using an explicit ResourceBuilder.
        /// </summary>
        public static TracerProviderBuilder AddLogDBExporter(
            this TracerProviderBuilder builder,
            ResourceBuilder resourceBuilder,
            Action<LogDBExporterOptions>? configure = null)
        {
            if (resourceBuilder == null)
            {
                throw new ArgumentNullException(nameof(resourceBuilder));
            }

            return builder
                .SetResourceBuilder(resourceBuilder)
                .AddLogDBExporter(options =>
                {
                    options.Resource = resourceBuilder.Build();
                    configure?.Invoke(options);
                });
        }

        /// <summary>
        /// Adds LogDB exporter to the MeterProviderBuilder
        /// </summary>
        public static MeterProviderBuilder AddLogDBExporter(
            this MeterProviderBuilder builder,
            Action<LogDBExporterOptions>? configure = null)
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));

            var options = BuildAndValidateOptions(configure);

            builder.ConfigureResource(resourceBuilder =>
            {
                options.Resource = resourceBuilder.Build();
                options.WriteDebug("Captured metrics resource from MeterProviderBuilder.");
            });

            var intervalMilliseconds = options.MetricExportIntervalMilliseconds > 0
                ? options.MetricExportIntervalMilliseconds
                : options.BatchExportProcessorOptions?.ScheduledDelayMilliseconds ?? 60000;

            var timeoutMilliseconds = options.MetricExportTimeoutMilliseconds > 0
                ? options.MetricExportTimeoutMilliseconds
                : options.BatchExportProcessorOptions?.ExporterTimeoutMilliseconds ?? 30000;

            builder.ConfigureServices(services =>
            {
                services.Configure<MetricReaderOptions>(readerOptions =>
                {
                    readerOptions.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = intervalMilliseconds;
                    readerOptions.PeriodicExportingMetricReaderOptions.ExportTimeoutMilliseconds = timeoutMilliseconds;

                    if (options.MetricTemporalityPreference.HasValue)
                    {
                        readerOptions.TemporalityPreference = options.MetricTemporalityPreference.Value;
                    }
                });
            });

            return builder.AddReader(new PeriodicExportingMetricReader(
                new LogDBMetricExporter(options),
                intervalMilliseconds,
                timeoutMilliseconds));
        }

        /// <summary>
        /// Adds LogDB exporter to the MeterProviderBuilder using an explicit ResourceBuilder.
        /// </summary>
        public static MeterProviderBuilder AddLogDBExporter(
            this MeterProviderBuilder builder,
            ResourceBuilder resourceBuilder,
            Action<LogDBExporterOptions>? configure = null)
        {
            if (resourceBuilder == null)
            {
                throw new ArgumentNullException(nameof(resourceBuilder));
            }

            return builder
                .SetResourceBuilder(resourceBuilder)
                .AddLogDBExporter(options =>
                {
                    options.Resource = resourceBuilder.Build();
                    configure?.Invoke(options);
                });
        }

        /// <summary>
        /// Adds LogDB exporter to the OpenTelemetryLoggerOptions
        /// </summary>
        public static OpenTelemetryLoggerOptions AddLogDBExporter(
            this OpenTelemetryLoggerOptions loggerOptions,
            Action<LogDBExporterOptions>? configure = null)
        {
            if (loggerOptions == null)
                throw new ArgumentNullException(nameof(loggerOptions));

            var options = BuildAndValidateOptions(configure);
            var exporter = new LogDBLogExporter(options);

            if (options.ExportProcessorType == ExportProcessorType.Simple)
            {
                return loggerOptions.AddProcessor(new SimpleLogRecordExportProcessor(exporter));
            }

            return loggerOptions.AddProcessor(new BatchLogRecordExportProcessor(
                exporter,
                options.BatchExportProcessorOptions?.MaxQueueSize ?? 2048,
                options.BatchExportProcessorOptions?.ScheduledDelayMilliseconds ?? 1000,
                options.BatchExportProcessorOptions?.ExporterTimeoutMilliseconds ?? 30000,
                options.BatchExportProcessorOptions?.MaxExportBatchSize ?? 512));
        }

        /// <summary>
        /// Adds LogDB exporter to OpenTelemetry logging using an explicit ResourceBuilder.
        /// </summary>
        public static OpenTelemetryLoggerOptions AddLogDBExporter(
            this OpenTelemetryLoggerOptions loggerOptions,
            ResourceBuilder resourceBuilder,
            Action<LogDBExporterOptions>? configure = null)
        {
            if (loggerOptions == null)
            {
                throw new ArgumentNullException(nameof(loggerOptions));
            }

            if (resourceBuilder == null)
            {
                throw new ArgumentNullException(nameof(resourceBuilder));
            }

            loggerOptions.SetResourceBuilder(resourceBuilder);
            return loggerOptions.AddLogDBExporter(options =>
            {
                options.Resource = resourceBuilder.Build();
                configure?.Invoke(options);
            });
        }

        private static LogDBExporterOptions BuildAndValidateOptions(Action<LogDBExporterOptions>? configure)
        {
            var options = new LogDBExporterOptions();
            options.ConfigureFromEnvironment();
            configure?.Invoke(options);

            if (string.IsNullOrWhiteSpace(options.ApiKey))
            {
                throw new ArgumentException("LogDB API key is required");
            }

            var identity = options.ResolveIdentity(force: true);
            options.WriteDebug(
                $"Using service '{identity.ServiceName}', environment '{identity.Environment}', endpoint '{options.Endpoint ?? "auto"}'.");
            return options;
        }
    }
}

