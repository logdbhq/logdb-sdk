using System;
using System.Threading.Tasks;
using LogDB.Extensions.Logging;

namespace LogDB.Examples
{
    /// <summary>
    /// Examples demonstrating how to use the LogDB Reader
    /// </summary>
    public static class ReaderExample
    {
        public static async Task BasicUsageExample()
        {
            // Optional: fetch discovered URL explicitly (for diagnostics/pinning)
            var readerServiceUrl = await LogDBReaderExtensions.DiscoverReaderServiceUrlAsync();

            // Create a reader instance (auto-discovery also works when URL is not provided)
            var reader = LogDBReaderExtensions.CreateReader("your-api-key", readerServiceUrl);

            // Query logs with fluent API
            var errorLogs = await reader.QueryLogs()
                .FromApplication("MyApp")
                .InEnvironment("Production")
                .WithLevel("Error")
                .InLastHours(24)
                .Take(100)
                .ExecuteAsync();

            Console.WriteLine($"Found {errorLogs.TotalCount} error logs");

            foreach (var log in errorLogs.Items)
            {
                Console.WriteLine($"[{log.Timestamp}] {log.Level}: {log.Message}");
            }
        }

        public static async Task QueryWithFiltersExample()
        {
            var reader = LogDBReaderExtensions.CreateReader("your-api-key");

            // Query logs with multiple filters
            var logs = await reader.GetLogsAsync(new LogQueryParams
            {
                Application = "OrderService",
                Environment = "Production",
                Level = "Error",
                FromDate = DateTime.UtcNow.AddDays(-7),
                ToDate = DateTime.UtcNow,
                SearchString = "payment",
                Take = 50,
                SortField = "Timestamp",
                SortAscending = false
            });

            Console.WriteLine($"Found {logs.TotalCount} logs matching criteria");
        }

        public static async Task QueryCacheExample()
        {
            var reader = LogDBReaderExtensions.CreateReader("your-api-key");

            // Get a specific cache value
            var sessionData = await reader.GetLogCacheAsync("session:user123");
            if (sessionData != null)
            {
                Console.WriteLine($"Session data: {sessionData.Value}");
            }

            // Query cache entries with pattern
            var allUserSessions = await reader.QueryCache()
                .WithKeyPattern("session:*")
                .InLastHours(1)
                .ExecuteAsync();

            Console.WriteLine($"Active sessions: {allUserSessions.TotalCount}");
        }

        public static async Task QueryMetricsExample()
        {
            var reader = LogDBReaderExtensions.CreateReader("your-api-key");

            // Query metrics (LogPoints)
            var cpuMetrics = await reader.QueryLogPoints()
                .ForMeasurement("cpu_usage")
                .WithTag("host", "server-1")
                .InLastHours(4)
                .Take(100)
                .ExecuteAsync();

            foreach (var point in cpuMetrics.Items)
            {
                Console.WriteLine($"[{point.Timestamp}] CPU: {point.Fields.FirstOrDefault()?.Value}%");
            }
        }

        public static async Task QueryRelationsExample()
        {
            var reader = LogDBReaderExtensions.CreateReader("your-api-key");

            // Query relations
            var userOrders = await reader.QueryRelations()
                .FromOrigin("user:123")
                .WithRelation("placed")
                .InLastDays(30)
                .ExecuteAsync();

            Console.WriteLine($"User 123 placed {userOrders.TotalCount} orders");

            // Get related entities (graph traversal)
            var relatedProducts = await reader.GetRelatedEntitiesAsync(
                entity: "order:456",
                direction: "outgoing",
                relationType: "contains",
                depth: 2
            );

            Console.WriteLine($"Order 456 contains {relatedProducts.Count} related items");
        }

        public static async Task GetStatsExample()
        {
            var reader = LogDBReaderExtensions.CreateReader("your-api-key");

            // Get log statistics
            var stats = await reader.GetLogStatsAsync(new LogStatsParams
            {
                Collection = "production-logs",
                FromDate = DateTime.UtcNow.AddDays(-7),
                GroupBy = "level"
            });

            Console.WriteLine($"Total logs: {stats.TotalCount}");
            Console.WriteLine($"Errors: {stats.ErrorCount} ({(double)stats.ErrorCount / stats.TotalCount * 100:F1}%)");
            Console.WriteLine($"Warnings: {stats.WarningCount}");
            Console.WriteLine($"Info: {stats.InfoCount}");

            foreach (var group in stats.Groups)
            {
                Console.WriteLine($"  {group.Key}: {group.Count} ({group.Percentage:F1}%)");
            }
        }

        public static async Task GetTimeSeriesExample()
        {
            var reader = LogDBReaderExtensions.CreateReader("your-api-key");

            // Get time series data for charting
            var timeSeries = await reader.GetLogTimeSeriesAsync(new LogTimeSeriesParams
            {
                Collection = "production-logs",
                FromDate = DateTime.UtcNow.AddDays(-7),
                Interval = "day",
                GroupBy = "level"
            });

            Console.WriteLine("Log count by day and level:");
            foreach (var point in timeSeries)
            {
                Console.WriteLine($"  {point.Timestamp:yyyy-MM-dd} [{point.Group}]: {point.Count}");
            }
        }

        public static async Task FluentBuilderAdvancedExample()
        {
            var reader = LogDBReaderExtensions.CreateReader("your-api-key");

            // Complex query using fluent builder
            var criticalLogs = await reader.QueryLogs()
                .FromApplication("PaymentService")
                .InEnvironment("Production")
                .WithLevel("Error")
                .OnlyExceptions()
                .Containing("timeout")
                .WithLabel("critical")
                .WithAttribute("region", "us-east-1")
                .InDateRange(DateTime.UtcNow.AddHours(-2), DateTime.UtcNow)
                .OrderByTimestamp(ascending: false)
                .Skip(0)
                .Take(20)
                .ExecuteAsync();

            Console.WriteLine($"Found {criticalLogs.TotalCount} critical payment errors");

            // Get count only
            var count = await reader.QueryLogs()
                .FromApplication("PaymentService")
                .WithLevel("Error")
                .InLastHours(1)
                .CountAsync();

            Console.WriteLine($"Errors in the last hour: {count}");

            // Get first matching log
            var firstError = await reader.QueryLogs()
                .WithLevel("Error")
                .InLastMinutes(5)
                .OrderByTimestamp(ascending: false)
                .FirstOrDefaultAsync();

            if (firstError != null)
            {
                Console.WriteLine($"Latest error: {firstError.Message}");
            }
        }

        public static async Task GetDistinctValuesExample()
        {
            var reader = LogDBReaderExtensions.CreateReader("your-api-key");

            // Get list of all applications
            var applications = await reader.GetDistinctValuesAsync("application");
            Console.WriteLine("Applications: " + string.Join(", ", applications));

            // Get environments for a specific collection
            var environments = await reader.GetDistinctValuesAsync("environment", "production-logs");
            Console.WriteLine("Environments: " + string.Join(", ", environments));

            // Get available log levels
            var levels = await reader.GetDistinctValuesAsync("level");
            Console.WriteLine("Log levels: " + string.Join(", ", levels));
        }
    }
}


