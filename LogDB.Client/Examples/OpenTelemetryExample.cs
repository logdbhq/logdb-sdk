using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using LogDB.OpenTelemetry;
using LogDB.Extensions.Logging;

namespace LogDB.Examples
{
    /// <summary>
    /// Example showing how to use LogDB with OpenTelemetry
    /// </summary>
    public class OpenTelemetryExample
    {
        private static readonly ActivitySource ActivitySource = new("LogDB.Examples");
        private static readonly System.Diagnostics.Metrics.Meter Meter = new("LogDB.Examples");
        private static readonly System.Diagnostics.Metrics.Counter<long> RequestCounter = 
            Meter.CreateCounter<long>("requests_total", "requests", "Total number of requests");

        public static async Task RunAsync()
        {
            // Configure OpenTelemetry
            var resourceBuilder = ResourceBuilder.CreateDefault()
                .AddService("example-service", serviceVersion: "1.0.0")
                .AddAttributes(new[]
                {
                    new KeyValuePair<string, object>("deployment.environment", "development"),
                    new KeyValuePair<string, object>("deployment.environment.name", "development"),
                    new KeyValuePair<string, object>("host.name", Environment.MachineName)
                });

            // Configure tracing
            using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                .AddSource("LogDB.Examples")
                .AddLogDBExporter(resourceBuilder, options =>
                {
                    options.ApiKey = "your-api-key-here";
                    options.DefaultCollection = "traces";
                    options.Protocol = LogDBProtocol.OpenTelemetry; // Use OTLP
                    options.Endpoint = "http://localhost:4317"; // Replace with your OTLP endpoint or set OTEL_EXPORTER_OTLP_ENDPOINT env var
                })
                .Build();

            // Configure metrics
            using var meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddMeter("LogDB.Examples")
                .AddLogDBExporter(resourceBuilder, options =>
                {
                    options.ApiKey = "your-api-key-here";
                    options.DefaultCollection = "metrics";
                    options.MetricExportIntervalMilliseconds = 30000;
                })
                .Build();

            // Configure logging with OpenTelemetry
            var services = new ServiceCollection();
            services.AddLogging(builder =>
            {
                builder
                    .SetMinimumLevel(LogLevel.Debug)
                    .AddOpenTelemetry(options =>
                    {
                        options.AddLogDBExporter(resourceBuilder, exporterOptions =>
                        {
                            exporterOptions.ApiKey = "your-api-key-here";
                            exporterOptions.DefaultCollection = "logs";
                        });
                    });
            });

            var serviceProvider = services.BuildServiceProvider();
            var logger = serviceProvider.GetRequiredService<ILogger<OpenTelemetryExample>>();

            // Example: Distributed tracing
            using (var activity = ActivitySource.StartActivity("ProcessOrder", ActivityKind.Server))
            {
                activity?.SetTag("order.id", "ORDER-123");
                activity?.SetTag("customer.id", "CUST-456");

                logger.LogInformation("Processing order {OrderId}", "ORDER-123");

                // Child span
                using (var childActivity = ActivitySource.StartActivity("ValidateOrder", ActivityKind.Internal))
                {
                    childActivity?.SetTag("validation.result", "success");
                    logger.LogInformation("Order validation completed");
                }

                // Record metric
                RequestCounter.Add(1, 
                    new KeyValuePair<string, object?>("endpoint", "/api/orders"),
                    new KeyValuePair<string, object?>("method", "POST"),
                    new KeyValuePair<string, object?>("status", "200"));

                // Simulate some work
                await Task.Delay(100);

                activity?.SetStatus(ActivityStatusCode.Ok);
                logger.LogInformation("Order processed successfully");
            }

            // Example: Error handling with tracing
            using (var activity = ActivitySource.StartActivity("ProcessPayment", ActivityKind.Client))
            {
                try
                {
                    throw new InvalidOperationException("Payment gateway error");
                }
                catch (Exception ex)
                {
                    activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                    activity?.RecordException(ex);
                    logger.LogError(ex, "Payment processing failed");
                    
                    RequestCounter.Add(1,
                        new KeyValuePair<string, object?>("endpoint", "/api/payments"),
                        new KeyValuePair<string, object?>("method", "POST"),
                        new KeyValuePair<string, object?>("status", "500"));
                }
            }

            // Example: Custom metrics
            var histogram = Meter.CreateHistogram<double>("response_time", "milliseconds", "Response time histogram");
            histogram.Record(125.5, 
                new KeyValuePair<string, object?>("endpoint", "/api/products"),
                new KeyValuePair<string, object?>("method", "GET"));

            // Wait for exports
            await Task.Delay(2000);

            serviceProvider.Dispose();
        }
    }
}
