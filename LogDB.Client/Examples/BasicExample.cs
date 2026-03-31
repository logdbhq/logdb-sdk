using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using LogDB.Extensions.Logging;
using LogDB.Extensions.Logging.Enrichers;

namespace LogDB.Examples
{
    /// <summary>
    /// Basic example showing how to use LogDB with Microsoft.Extensions.Logging
    /// </summary>
    public class BasicExample
    {
        public static async Task RunAsync()
        {
            // Configure services
            var services = new ServiceCollection();

            // Add logging with LogDB
            services.AddLogging(builder =>
            {
                builder
                    .SetMinimumLevel(LogLevel.Debug)
                    .AddConsole() // Also log to console for demo
                    .AddLogDB(options =>
                    {
                        options.ApiKey = "your-api-key-here";
                        options.DefaultCollection = "example-app";
                        options.DefaultEnvironment = "development";
                        
                        // Add enrichers
                        options.AddEnricher(new MachineNameEnricher());
                        options.AddEnricher(new EnvironmentEnricher());
                        options.AddEnricher(new ThreadEnricher());
                        
                        // Custom enricher
                        options.AddEnricher(log =>
                        {
                            log.AttributesS["app.version"] = "1.0.0";
                            log.AttributesS["deployment.region"] = "us-west-2";
                        });
                    });
            });

            // Build service provider
            var serviceProvider = services.BuildServiceProvider();

            // Get logger
            var logger = serviceProvider.GetRequiredService<ILogger<BasicExample>>();

            // Simple logging
            logger.LogInformation("Application started");

            // Structured logging
            logger.LogInformation("Processing order {OrderId} for customer {CustomerId}", 
                12345, "CUST-789");

            // Log with exception
            try
            {
                throw new InvalidOperationException("Something went wrong!");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to process order {OrderId}", 12345);
            }

            // Using scopes
            using (logger.BeginScope(new { TransactionId = Guid.NewGuid(), UserId = 456 }))
            {
                logger.LogInformation("Starting transaction");
                logger.LogInformation("Processing payment");
                logger.LogInformation("Transaction completed");
            }

            // Different log levels
            logger.LogTrace("This is a trace message");
            logger.LogDebug("Debug information: {DebugData}", new { Key = "Value" });
            logger.LogInformation("Information message");
            logger.LogWarning("Warning: Low memory");
            logger.LogError("Error occurred");
            logger.LogCritical("Critical system failure");

            // Wait a bit for logs to be sent
            await Task.Delay(2000);

            // Dispose to ensure all logs are flushed
            serviceProvider.Dispose();
        }
    }
}

