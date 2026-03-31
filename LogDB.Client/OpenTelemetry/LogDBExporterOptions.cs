using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using LogDB.Extensions.Logging;
using OTelResource = OpenTelemetry.Resources.Resource;

namespace LogDB.OpenTelemetry
{
    /// <summary>
    /// Options for configuring LogDB OpenTelemetry exporter
    /// </summary>
    public class LogDBExporterOptions
    {
        private const string FallbackServiceName = "logdb-dotnet-app";
        private const string FallbackEnvironment = "production";

        private readonly object _identitySync = new();
        private string? _endpoint;
        private OTelResource? _resource;

        /// <summary>
        /// LogDB API key for authentication
        /// </summary>
        public string ApiKey { get; set; } = string.Empty;

        /// <summary>
        /// Endpoint for LogDB service
        /// </summary>
        public string? Endpoint
        {
            get => _endpoint;
            set => _endpoint = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        /// <summary>
        /// Alias for Endpoint. Keeps naming consistent with ServiceUrl usage in other LogDB SDK APIs.
        /// </summary>
        public string? ServiceUrl
        {
            get => Endpoint;
            set => Endpoint = value;
        }

        /// <summary>
        /// Protocol to use for communication
        /// </summary>
        public LogDBProtocol Protocol { get; set; } = LogDBProtocol.Native;

        /// <summary>
        /// Default collection name
        /// </summary>
        public string? DefaultCollection { get; set; }

        /// <summary>
        /// Default environment name
        /// </summary>
        public string? DefaultEnvironment { get; set; }

        /// <summary>
        /// Service name
        /// </summary>
        public string? ServiceName { get; set; }

        /// <summary>
        /// OpenTelemetry Resource used to resolve service and environment identity.
        /// </summary>
        public OTelResource? Resource
        {
            get => _resource;
            set
            {
                _resource = value;
                ResolveIdentity(force: true);
            }
        }

        /// <summary>
        /// Resolved service name used by exporters. Populated by ResolveIdentity().
        /// </summary>
        public string ResolvedServiceName { get; private set; } = FallbackServiceName;

        /// <summary>
        /// Resolved environment used by exporters. Populated by ResolveIdentity().
        /// </summary>
        public string ResolvedEnvironment { get; private set; } = FallbackEnvironment;

        /// <summary>
        /// Export processor type (Simple or Batch)
        /// </summary>
        public ExportProcessorType ExportProcessorType { get; set; } = ExportProcessorType.Batch;

        /// <summary>
        /// Batch export processor options
        /// </summary>
        public BatchExportProcessorOptions<Activity>? BatchExportProcessorOptions { get; set; }

        /// <summary>
        /// Exporter timeout in milliseconds
        /// </summary>
        public int ExporterTimeoutMilliseconds { get; set; } = 30000;

        /// <summary>
        /// Additional headers to send with requests
        /// </summary>
        public Dictionary<string, string> Headers { get; set; } = new();

        /// <summary>
        /// Enable debug logging for exporter internals.
        /// </summary>
        public bool EnableDebugLogging { get; set; } = false;

        /// <summary>
        /// Optional callback for debug log output.
        /// </summary>
        public Action<string>? DebugLogger { get; set; }

        /// <summary>
        /// Optional callback invoked when exporter operations fail.
        /// </summary>
        public Action<Exception>? OnError { get; set; }

        /// <summary>
        /// Metrics export interval for PeriodicExportingMetricReader.
        /// </summary>
        public int MetricExportIntervalMilliseconds { get; set; } = 60000;

        /// <summary>
        /// Metrics export timeout for PeriodicExportingMetricReader.
        /// </summary>
        public int MetricExportTimeoutMilliseconds { get; set; } = 30000;

        /// <summary>
        /// Preferred metric temporality.
        /// </summary>
        public MetricReaderTemporalityPreference? MetricTemporalityPreference { get; set; }

        /// <summary>
        /// Configure options from environment variables
        /// </summary>
        public void ConfigureFromEnvironment()
        {
            SetFromEnvironment("LOGDB_API_KEY", value => ApiKey = value);
            SetFromFirstEnvironment(new[] { "LOGDB_ENDPOINT", "LOGDB_SERVICE_URL" }, value => Endpoint = value);
            SetFromEnvironment("LOGDB_DEFAULT_COLLECTION", value => DefaultCollection = value);
            SetFromFirstEnvironment(new[] { "LOGDB_DEFAULT_ENVIRONMENT", "LOGDB_ENVIRONMENT" }, value => DefaultEnvironment = value);
            SetFromFirstEnvironment(new[] { "LOGDB_DEFAULT_APPLICATION", "OTEL_SERVICE_NAME" }, value => ServiceName = value);

            var protocolStr = Environment.GetEnvironmentVariable("LOGDB_PROTOCOL");
            if (Enum.TryParse<LogDBProtocol>(protocolStr, true, out var protocol))
                Protocol = protocol;

            ResolveIdentity(force: true);
        }

        /// <summary>
        /// Resolves service and environment identity deterministically.
        /// </summary>
        public (string ServiceName, string Environment) ResolveIdentity(bool force = false)
        {
            lock (_identitySync)
            {
                if (!force &&
                    !string.IsNullOrWhiteSpace(ResolvedServiceName) &&
                    !string.IsNullOrWhiteSpace(ResolvedEnvironment))
                {
                    return (ResolvedServiceName, ResolvedEnvironment);
                }

                var resourceServiceName = GetResourceAttribute(Resource, "service.name");
                var resourceEnvironment =
                    GetResourceAttribute(Resource, "deployment.environment") ??
                    GetResourceAttribute(Resource, "deployment.environment.name");

                var otelResourceServiceName = GetOtelResourceAttribute("service.name");
                var otelResourceEnvironment =
                    GetOtelResourceAttribute("deployment.environment") ??
                    GetOtelResourceAttribute("deployment.environment.name");

                ResolvedServiceName = FirstNonEmpty(
                    resourceServiceName,
                    ServiceName,
                    Environment.GetEnvironmentVariable("LOGDB_DEFAULT_APPLICATION"),
                    otelResourceServiceName,
                    Environment.GetEnvironmentVariable("OTEL_SERVICE_NAME"),
                    GetEntryAssemblyName(),
                    FallbackServiceName)!;

                ResolvedEnvironment = FirstNonEmpty(
                    resourceEnvironment,
                    DefaultEnvironment,
                    Environment.GetEnvironmentVariable("LOGDB_DEFAULT_ENVIRONMENT"),
                    otelResourceEnvironment,
                    FallbackEnvironment)!;

                return (ResolvedServiceName, ResolvedEnvironment);
            }
        }

        internal void WriteDebug(string message)
        {
            if (!EnableDebugLogging)
            {
                return;
            }

            var normalized = $"[LogDB.OpenTelemetry] {message}";
            if (DebugLogger != null)
            {
                try
                {
                    DebugLogger(normalized);
                    return;
                }
                catch
                {
                    // Ignore callback failures to avoid affecting export path.
                }
            }

            Console.Error.WriteLine(normalized);
        }

        internal void ReportError(Exception exception, string operation)
        {
            WriteDebug($"{operation} failed: {exception.GetType().Name}: {exception.Message}");
            try
            {
                OnError?.Invoke(exception);
            }
            catch
            {
                // Never throw from error callback.
            }
        }

        private static void SetFromEnvironment(string name, Action<string> assign)
        {
            var value = Environment.GetEnvironmentVariable(name);
            if (!string.IsNullOrWhiteSpace(value))
            {
                assign(value.Trim());
            }
        }

        private static void SetFromFirstEnvironment(string[] names, Action<string> assign)
        {
            foreach (var name in names)
            {
                var value = Environment.GetEnvironmentVariable(name);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    assign(value.Trim());
                    return;
                }
            }
        }

        private static string? GetResourceAttribute(OTelResource? resource, string key)
        {
            if (resource == null)
            {
                return null;
            }

            foreach (var attribute in resource.Attributes)
            {
                if (string.Equals(attribute.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    return attribute.Value?.ToString();
                }
            }

            return null;
        }

        private static string? GetOtelResourceAttribute(string key)
        {
            var resourceAttributes = Environment.GetEnvironmentVariable("OTEL_RESOURCE_ATTRIBUTES");
            if (string.IsNullOrWhiteSpace(resourceAttributes))
            {
                return null;
            }

#if NETFRAMEWORK
            var pairs = resourceAttributes.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
#else
            var pairs = resourceAttributes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
#endif
            foreach (var pair in pairs)
            {
                var separatorIndex = pair.IndexOf('=');
                if (separatorIndex <= 0 || separatorIndex >= pair.Length - 1)
                {
                    continue;
                }

                var pairKey = pair.Substring(0, separatorIndex).Trim();
                if (!string.Equals(pairKey, key, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var pairValue = pair.Substring(separatorIndex + 1).Trim();
                if (!string.IsNullOrWhiteSpace(pairValue))
                {
                    return pairValue;
                }
            }

            return null;
        }

        private static string? FirstNonEmpty(params string?[] values)
        {
            return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        }

        private static string? GetEntryAssemblyName()
        {
            var entryAssemblyName = Assembly.GetEntryAssembly()?.GetName().Name;
            if (!string.IsNullOrWhiteSpace(entryAssemblyName))
            {
                return entryAssemblyName;
            }

            var processName = Process.GetCurrentProcess().ProcessName;
            return string.IsNullOrWhiteSpace(processName) ? null : processName;
        }
    }
}
