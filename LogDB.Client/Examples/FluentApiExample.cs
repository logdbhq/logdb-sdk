using System;
using System.Threading.Tasks;
using com.logdb.logger;
using com.logdb.LogDB;
using com.logdb.LogDB.LogBuilders;
using com.logdb.logger.LogBuilders;
using LogDB.Client.Models;

namespace LogDB.Examples
{
    /// <summary>
    /// Example showing how to use the fluent API (legacy compatibility)
    /// </summary>
    public class FluentApiExample
    {
        public static async Task RunAsync()
        {
            // Configure API key globally
            LogEventBuilder.ApiKey = "your-api-key-here";
            LogEventBuilder.Collection = "example-app";
            LogEventBuilder.Environment = "development";
            LogEventBuilder.Application = "FluentApiExample";

            // Simple log
            await LogEventBuilder.Create()
                .SetMessage("Application started")
                .SetLogLevel(LogLevel.Info)
                .Log();

            // Structured log with attributes
            await LogEventBuilder.Create()
                .SetMessage("Processing order")
                .SetLogLevel(LogLevel.Info)
                .AddAttribute("order.id", "ORDER-123")
                .AddAttribute("customer.id", "CUST-456")
                .AddAttribute("order.total", "299.99")
                .AddAttribute("order.isPriority", true)
                .AddAttribute("order.date", DateTime.UtcNow)
                .AddLabel("order")
                .AddLabel("processing")
                .Log();

            // Log with user context
            await LogEventBuilder.Create()
                .SetMessage("User logged in")
                .SetLogLevel(LogLevel.Info)
                .SetUserEmail("user@example.com")
                .SetIpAddress("192.168.1.100")
                .SetRequestPath("/api/auth/login")
                .SetHttpMethod("POST")
                .SetStatusCode(200)
                .SetCorrelationId(Guid.NewGuid().ToString())
                .Log();

            // Error logging
            try
            {
                throw new InvalidOperationException("Database connection failed");
            }
            catch (Exception ex)
            {
                await LogEventBuilder.Create()
                    .SetException(ex)
                    .SetSource("DatabaseService")
                    .AddAttribute("database.name", "orders_db")
                    .AddAttribute("retry.count", 3)
                    .Log();
            }

            // Encrypted log
            await LogEventBuilder.Create()
                .SetMessage("Sensitive user data")
                .SetLogLevel(LogLevel.Info)
                .AddAttribute("ssn", "123-45-6789", isEncrypted: true)
                .AddAttribute("credit_card", "4111-1111-1111-1111", isEncrypted: true)
                .Encrypt() // Encrypt entire log
                .Log();

            // Heartbeat using LogBeat
            await LogBeatBuilder.Create()
                .SetMeasurement("service_heartbeat")
                .SetApplication("order-processor")
                .SetEnvironment("production")
                .SetLogLevel(LogLevel.Info)
                .SetCollection("heartbeats")
                .Log();

            // Cache entry
            await LogCacheBuilder.Create()
                .SetKey("user:12345:preferences")
                .SetValue(@"{""theme"":""dark"",""language"":""en"",""notifications"":true}")
                .Log();

            // LogPoint and LogRelation are intentionally omitted here because
            // they are marked [Soon] and throw in the current public SDK build.

            // Batch operations with custom collection
            await LogEventBuilder.Create()
                .SetCollection("audit-logs")
                .SetMessage("Admin action performed")
                .SetLogLevel(LogLevel.Warning)
                .AddAttribute("admin.id", "ADMIN-001")
                .AddAttribute("action", "USER_DELETED")
                .AddAttribute("target.user", "USER-999")
                .Log();

            Console.WriteLine("Fluent API examples completed");
        }
    }
}
