using System;
using NLog;
using NLog.Config;

namespace LogDB.NLog.Examples
{
    /// <summary>
    /// Simple example demonstrating basic LogDB NLog target usage
    /// </summary>
    public class SimpleExample
    {
        public static void Run()
        {
            // Create logging configuration
            var config = new LoggingConfiguration();

            // Create LogDB target
            var logDBTarget = new LogDBTarget
            {
                ApiKey = "your-api-key-here",
                DefaultApplication = "ExampleApp",
                DefaultEnvironment = "Development",
                DefaultPayloadType = LogDBPayloadType.Log,
                MinimumLevel = LogLevel.Info
            };

            // Add target to configuration
            config.AddTarget("logdb", logDBTarget);

            // Add rule to send logs to LogDB
            config.AddRule(LogLevel.Info, LogLevel.Fatal, "logdb");

            // Apply configuration
            LogManager.Configuration = config;

            // Get logger
            var logger = LogManager.GetCurrentClassLogger();

            // Use the logger
            logger.Info("Application started");
            logger.Info("User {0} logged in from {1}", 12345, "192.168.1.1");
            
            logger.Warn("This is a warning message");

            // Route event to LogCache via control properties
            var cacheEvent = new LogEventInfo(LogLevel.Info, "Demo", "Cache upsert");
            cacheEvent.Properties["LogDBType"] = LogDBPayloadType.Cache;
            cacheEvent.Properties["LogDBCacheKey"] = "sample:session:nlog:12345";
            cacheEvent.Properties["LogDBCacheValue"] = "active";
            cacheEvent.Properties["LogDBTtlSeconds"] = 600;
            logger.Log(cacheEvent);

            // Route event to LogBeat via control properties
            var beatEvent = new LogEventInfo(LogLevel.Info, "Demo", "Heartbeat");
            beatEvent.Properties["LogDBType"] = LogDBPayloadType.Beat;
            beatEvent.Properties["LogDBMeasurement"] = "sample_worker_health";
            beatEvent.Properties["Tag.worker"] = "nlog-worker-1";
            beatEvent.Properties["Field.status"] = "ok";
            beatEvent.Properties["Field.latency_ms"] = 11.4;
            logger.Log(beatEvent);
            
            try
            {
                throw new InvalidOperationException("Something went wrong");
            }
            catch (Exception ex)
            {
                logger.Error(ex, "An error occurred while processing");
            }

            // Flush and shutdown
            LogManager.Shutdown();
        }
    }
}






