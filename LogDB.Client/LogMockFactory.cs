using LogDB.Client.Models;
using Newtonsoft.Json;

public static class LogMockFactory
{
    private static readonly Random _random = new();

    private static readonly string[] Applications = ["LogDB.API", "LogDB.Worker", "LogDB.Admin"];
    private static readonly string[] Environments = ["Development", "Staging", "Production"];
    private static readonly string[] HttpMethods = ["GET", "POST", "PUT", "DELETE"];
    private static readonly string[] RequestPaths = ["/api/users", "/api/orders/42", "/health", "/api/auth/login"];
    private static readonly string[] Sources = ["UserService", "OrderProcessor", "AuthModule", "HealthCheck"];
    private static readonly string[] UserEmails = ["alice@example.com", "bob@example.net", "carol@sample.org"];
    private static readonly LogLevel[] Levels = [LogLevel.Info, LogLevel.Warning, LogLevel.Error, LogLevel.Critical];
    private static readonly string[] Collections = ["logs-prod", "logs-staging", "system-events", "audit-trail", "error-tracking", "user-activity"];
    private static readonly string[] LogPointCollections = ["metrics-prod", "metrics-staging", "system-monitoring", "performance-data", "infrastructure-metrics", "app-telemetry"];
    private static readonly string[] Measurements = ["cpu_usage", "memory_usage", "disk_io", "network_throughput", "response_time", "request_count", "error_rate"];
    private static readonly string[] Hosts = ["web-server-01", "web-server-02", "api-server-01", "db-server-01", "cache-server-01"];
    private static readonly string[] Regions = ["us-west-2", "us-east-1", "eu-west-1", "ap-southeast-1", "ca-central-1"];

    public static Log GenerateMockLog(string? apiKey = null)
    {
        var log = new Log
        {
            Timestamp = DateTime.UtcNow,
            Application = PickRandom(Applications),
            Environment = PickRandom(Environments),
            Level = PickRandom(Levels),
            Message = "Sample log message " + Guid.NewGuid().ToString("N").Substring(0, 8),
            Description = "This is a mock log entry used for testing.",
            Exception = _random.Next(0, 3) == 0
                ? "System.NullReferenceException: Object reference not set to an instance of an object."
                : null,
            StackTrace = _random.Next(0, 3) == 0 ? "at LogDB.Services.UserService.CreateUser()" : null,
            Source = PickRandom(Sources),
            UserId = _random.Next(1, 5000),
            UserEmail = PickRandom(UserEmails),
            CorrelationId = Guid.NewGuid().ToString(),
            RequestPath = PickRandom(RequestPaths),
            HttpMethod = PickRandom(HttpMethods),
            IpAddress = $"{_random.Next(10, 255)}.{_random.Next(0, 255)}.{_random.Next(0, 255)}.{_random.Next(1, 255)}",
            StatusCode = _random.Next(0, 2) == 0 ? 200 : _random.Next(400, 500),
            ApiKey = apiKey ?? "mock-api-key",
            AdditionalData = JsonConvert.SerializeObject(new
            {
                DebugId = Guid.NewGuid(),
                Retry = _random.Next(0, 3),
                Flags = new[] { "test", "mock" },
                Collection = PickRandom(Collections)
            }, Formatting.Indented)
        };

        return log;
    }

    public static LogPoint GenerateMockLogPoint(string? apiKey = null)
    {
        var measurement = PickRandom(Measurements);
        var logPoint = new LogPoint
        {
            ApiKey = apiKey ?? "mock-api-key",
            Collection = PickRandom(LogPointCollections),
            Measurement = measurement,
            Timestamp = DateTime.UtcNow.AddMinutes(-_random.Next(60))
        };

        // Add common tags (metadata)
        logPoint.Tag.Add(new LogMeta { Key = "host", Value = PickRandom(Hosts) });
        logPoint.Tag.Add(new LogMeta { Key = "environment", Value = PickRandom(Environments) });
        logPoint.Tag.Add(new LogMeta { Key = "application", Value = PickRandom(Applications) });
        logPoint.Tag.Add(new LogMeta { Key = "region", Value = PickRandom(Regions) });

        // Add measurement-specific fields (actual metric values)
        switch (measurement)
        {
            case "cpu_usage":
                logPoint.Field.Add(new LogMeta { Key = "percentage", Value = (_random.NextDouble() * 100).ToString("F2") });
                logPoint.Field.Add(new LogMeta { Key = "cores", Value = _random.Next(2, 16).ToString() });
                break;
            case "memory_usage":
                logPoint.Field.Add(new LogMeta { Key = "used_gb", Value = (_random.NextDouble() * 32).ToString("F2") });
                logPoint.Field.Add(new LogMeta { Key = "total_gb", Value = _random.Next(16, 64).ToString() });
                break;
            case "disk_io":
                logPoint.Field.Add(new LogMeta { Key = "read_mb_per_sec", Value = (_random.NextDouble() * 200).ToString("F2") });
                logPoint.Field.Add(new LogMeta { Key = "write_mb_per_sec", Value = (_random.NextDouble() * 100).ToString("F2") });
                break;
            case "network_throughput":
                logPoint.Field.Add(new LogMeta { Key = "inbound_mbps", Value = (_random.NextDouble() * 1000).ToString("F2") });
                logPoint.Field.Add(new LogMeta { Key = "outbound_mbps", Value = (_random.NextDouble() * 500).ToString("F2") });
                break;
            case "response_time":
                logPoint.Field.Add(new LogMeta { Key = "avg_ms", Value = (_random.NextDouble() * 1000).ToString("F2") });
                logPoint.Field.Add(new LogMeta { Key = "p95_ms", Value = (_random.NextDouble() * 2000).ToString("F2") });
                break;
            case "request_count":
                logPoint.Field.Add(new LogMeta { Key = "count", Value = _random.Next(1, 1000).ToString() });
                logPoint.Field.Add(new LogMeta { Key = "endpoint", Value = PickRandom(RequestPaths) });
                break;
            case "error_rate":
                logPoint.Field.Add(new LogMeta { Key = "percentage", Value = (_random.NextDouble() * 10).ToString("F2") });
                logPoint.Field.Add(new LogMeta { Key = "total_requests", Value = _random.Next(100, 10000).ToString() });
                break;
        }

        return logPoint;
    }

    public static T PickRandom<T>(T[] array)
    {
        return array[_random.Next(array.Length)];
    }
}