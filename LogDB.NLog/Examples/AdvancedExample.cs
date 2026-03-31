using System;
using System.Diagnostics;
using NLog;
using NLog.Config;

namespace LogDB.NLog.Examples
{
    /// <summary>
    /// Advanced example demonstrating structured logging, correlation, and HTTP context
    /// </summary>
    public class AdvancedExample
    {
        public static void Run()
        {
            var config = new LoggingConfiguration();

            // Create LogDB target with advanced options
            var logDBTarget = new LogDBTarget
            {
                ApiKey = "your-api-key-here",
                DefaultApplication = "AdvancedExample",
                DefaultEnvironment = "Production",
                DefaultCollection = "app-logs",
                DefaultPayloadType = LogDBPayloadType.Log,
                MinimumLevel = global::NLog.LogLevel.Info,
                
                // Batching configuration
                EnableBatching = true,
                BatchSize = 100,
                FlushIntervalSeconds = 5,
                
                // Reliability
                MaxRetries = 3,
                EnableCircuitBreaker = true,
                EnableCompression = true,
                
                // Custom filter
                Filter = logEvent =>
                    logEvent.Level.Ordinal >= global::NLog.LogLevel.Warn.Ordinal ||
                    logEvent.Properties.ContainsKey("Important")
            };

            config.AddTarget("logdb", logDBTarget);
            config.AddRule(global::NLog.LogLevel.Info, global::NLog.LogLevel.Fatal, "logdb");

            // Override Microsoft logs to Warning level
            config.AddRule(global::NLog.LogLevel.Warn, global::NLog.LogLevel.Fatal, "logdb", "Microsoft.*");

            LogManager.Configuration = config;
            var logger = LogManager.GetCurrentClassLogger();

            // Structured logging with properties
            var orderId = "ORD-12345";
            var amount = 99.99;
            var userId = 12345;
            
            var logEvent = new LogEventInfo(global::NLog.LogLevel.Info, "OrderService", "Order {OrderId} created for user {UserId} with amount {Amount}");
            logEvent.Properties["OrderId"] = orderId;
            logEvent.Properties["UserId"] = userId;
            logEvent.Properties["Amount"] = amount;
            logger.Log(logEvent);

            // Correlation ID
            var correlationId = Guid.NewGuid().ToString();
            var correlationEvent = new LogEventInfo(global::NLog.LogLevel.Info, "OrderService", "Processing order");
            correlationEvent.Properties["CorrelationId"] = correlationId;
            logger.Log(correlationEvent);
            
            // Activity/Trace support
            using var activity = new Activity("ProcessOrder");
            activity.Start();
            activity.SetTag("OrderId", orderId);

            var activityEvent = new LogEventInfo(global::NLog.LogLevel.Info, "OrderService", "Order processing started");
            activityEvent.Properties["TraceId"] = activity?.TraceId.ToString();
            activityEvent.Properties["SpanId"] = activity?.SpanId.ToString();
            logger.Log(activityEvent);
            
            // HTTP context simulation
            var httpEvent = new LogEventInfo(global::NLog.LogLevel.Info, "OrderService", "Request completed successfully");
            httpEvent.Properties["RequestPath"] = "/api/orders";
            httpEvent.Properties["HttpMethod"] = "POST";
            httpEvent.Properties["StatusCode"] = 200;
            httpEvent.Properties["IpAddress"] = "192.168.1.100";
            logger.Log(httpEvent);

            // Complex objects (will be serialized to JSON)
            var orderDetails = new
            {
                OrderId = orderId,
                Items = new[] { "Item1", "Item2" },
                Total = amount
            };
            
            var detailsEvent = new LogEventInfo(global::NLog.LogLevel.Info, "OrderService", "Order details");
            detailsEvent.Properties["OrderDetails"] = orderDetails;
            logger.Log(detailsEvent);

            // Exception with context
            try
            {
                throw new InvalidOperationException("Payment processing failed");
            }
            catch (Exception ex)
            {
                var errorEvent = new LogEventInfo(global::NLog.LogLevel.Error, "OrderService", "Failed to process payment for order {OrderId}");
                errorEvent.Properties["OrderId"] = orderId;
                errorEvent.Exception = ex;
                logger.Log(errorEvent);
            }

            // Cache and beat routing examples
            var cacheEvent = new LogEventInfo(global::NLog.LogLevel.Info, "OrderService", "Order cache state updated");
            cacheEvent.Properties["LogDBType"] = LogDBPayloadType.Cache;
            cacheEvent.Properties["LogDBCacheKey"] = $"order:last:{orderId}";
            cacheEvent.Properties["LogDBCacheValue"] = "paid";
            cacheEvent.Properties["LogDBTtlSeconds"] = 900;
            logger.Log(cacheEvent);

            var beatEvent = new LogEventInfo(global::NLog.LogLevel.Info, "OrderService", "Order pipeline heartbeat");
            beatEvent.Properties["LogDBType"] = LogDBPayloadType.Beat;
            beatEvent.Properties["LogDBMeasurement"] = "order_pipeline_health";
            beatEvent.Properties["Tag.component"] = "payment";
            beatEvent.Properties["Tag.region"] = "eu-central";
            beatEvent.Properties["Field.status"] = "ok";
            beatEvent.Properties["Field.queue_depth"] = 4;
            logger.Log(beatEvent);

            // Custom filtering example
            var normalEvent = new LogEventInfo(global::NLog.LogLevel.Info, "OrderService", "This won't be sent (filtered out)");
            logger.Log(normalEvent);

            var importantEvent = new LogEventInfo(global::NLog.LogLevel.Info, "OrderService", "This will be sent to LogDB");
            importantEvent.Properties["Important"] = true;
            logger.Log(importantEvent);

            LogManager.Shutdown();
        }
    }
}






