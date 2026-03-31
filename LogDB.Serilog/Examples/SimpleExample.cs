using System;
using Serilog;
using Serilog.Events;

namespace LogDB.Serilog.Examples
{
    /// <summary>
    /// Simple example demonstrating basic LogDB Serilog sink usage
    /// </summary>
    public class SimpleExample
    {
        public static void Run()
        {
            // Basic configuration
            // Note: To use .Console(), add Serilog.Sinks.Console package
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                // .WriteTo.Console() // Uncomment if you add Serilog.Sinks.Console package
                .WriteTo.LogDB(options =>
                {
                    options.ApiKey = "your-api-key-here";
                    options.DefaultApplication = "ExampleApp";
                    options.DefaultEnvironment = "Development";
                    options.DefaultPayloadType = LogDBPayloadType.Log;
                })
                .CreateLogger();

            // Use the logger
            Log.Information("Application started");
            Log.Information("User {UserId} logged in from {IpAddress}", 12345, "192.168.1.1");
            
            Log.Warning("This is a warning message");

            // Route event to LogCache via control properties
            Log.ForContext("LogDBType", LogDBPayloadType.Cache)
                .ForContext("LogDBCacheKey", "sample:session:serilog:12345")
                .ForContext("LogDBCacheValue", "active")
                .ForContext("LogDBTtlSeconds", 600)
                .Information("Cache upsert");

            // Route event to LogBeat via control properties
            Log.ForContext("LogDBType", LogDBPayloadType.Beat)
                .ForContext("LogDBMeasurement", "sample_worker_health")
                .ForContext("Tag.worker", "serilog-worker-1")
                .ForContext("Field.status", "ok")
                .ForContext("Field.latency_ms", 12.7)
                .Information("Heartbeat");
            
            try
            {
                throw new InvalidOperationException("Something went wrong");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "An error occurred while processing");
            }

            // Flush and close
            Log.CloseAndFlush();
        }
    }
}






