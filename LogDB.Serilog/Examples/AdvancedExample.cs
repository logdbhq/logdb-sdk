using System;
using System.Diagnostics;
using Serilog;
using Serilog.Context;
using Serilog.Events;

namespace LogDB.Serilog.Examples
{
    /// <summary>
    /// Advanced example demonstrating structured logging, correlation, and HTTP context
    /// </summary>
    public class AdvancedExample
    {
        public static void Run()
        {
            // Note: To use enrichers like .WithMachineName(), add Serilog.Enrichers.Environment package
            // Note: To use .Console(), add Serilog.Sinks.Console package
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .Enrich.FromLogContext()
                // .Enrich.WithMachineName() // Uncomment if you add Serilog.Enrichers.Environment package
                // .Enrich.WithThreadId() // Uncomment if you add Serilog.Enrichers.Thread package
                .Enrich.WithProperty("Application", "AdvancedExample")
                // .WriteTo.Console() // Uncomment if you add Serilog.Sinks.Console package
                .WriteTo.LogDB(options =>
                {
                    options.ApiKey = "your-api-key-here";
                    options.DefaultApplication = "AdvancedExample";
                    options.DefaultEnvironment = "Production";
                    options.DefaultCollection = "app-logs";
                    options.DefaultPayloadType = LogDBPayloadType.Log;
                    
                    // Batching configuration
                    options.EnableBatching = true;
                    options.BatchSize = 100;
                    options.FlushInterval = TimeSpan.FromSeconds(5);
                    
                    // Reliability
                    options.MaxRetries = 3;
                    options.EnableCircuitBreaker = true;
                    options.EnableCompression = true;
                    
                    // Error handling
                    options.OnError = (ex, logEvent) =>
                    {
                        Console.WriteLine($"Failed to send log to LogDB: {ex.Message}");
                    };
                })
                .CreateLogger();

            // Structured logging with properties
            var orderId = "ORD-12345";
            var amount = 99.99;
            var userId = 12345;
            
            Log.Information("Order {OrderId} created for user {UserId} with amount {Amount:C}", 
                orderId, userId, amount);

            // Correlation ID
            using (LogContext.PushProperty("CorrelationId", Guid.NewGuid().ToString()))
            {
                Log.Information("Processing order");
                
                // Activity/Trace support
                using var activity = new Activity("ProcessOrder");
                activity.Start();
                activity.SetTag("OrderId", orderId);
                
                Log.Information("Order processing started");
                
                // HTTP context simulation
                using (LogContext.PushProperty("RequestPath", "/api/orders"))
                using (LogContext.PushProperty("HttpMethod", "POST"))
                using (LogContext.PushProperty("StatusCode", 200))
                using (LogContext.PushProperty("IpAddress", "192.168.1.100"))
                {
                    Log.Information("Request completed successfully");
                }
            }

            // Complex objects
            var orderDetails = new
            {
                OrderId = orderId,
                Items = new[] { "Item1", "Item2" },
                Total = amount
            };
            
            Log.Information("Order details: {@OrderDetails}", orderDetails);

            // Exception with context
            try
            {
                throw new InvalidOperationException("Payment processing failed");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to process payment for order {OrderId}", orderId);
            }

            // Cache and beat routing examples
            Log.ForContext("LogDBType", LogDBPayloadType.Cache)
                .ForContext("LogDBCacheKey", $"order:last:{orderId}")
                .ForContext("LogDBCacheValue", "paid")
                .ForContext("LogDBTtlSeconds", 900)
                .Information("Order cache state updated");

            Log.ForContext("LogDBType", LogDBPayloadType.Beat)
                .ForContext("LogDBMeasurement", "order_pipeline_health")
                .ForContext("Tag.component", "payment")
                .ForContext("Tag.region", "eu-central")
                .ForContext("Field.status", "ok")
                .ForContext("Field.queue_depth", 4)
                .Information("Order pipeline heartbeat");

            // Custom filtering
            Log.Logger = new LoggerConfiguration()
                .WriteTo.LogDB(options =>
                {
                    options.ApiKey = "your-api-key-here";
                    options.Filter = logEvent =>
                    {
                        // Only send logs with "Important" property
                        return logEvent.Properties.ContainsKey("Important");
                    };
                })
                .CreateLogger();

            Log.Information("This won't be sent");
            
            using (LogContext.PushProperty("Important", true))
            {
                Log.Information("This will be sent to LogDB");
            }

            Log.CloseAndFlush();
        }
    }
}






