# LogDB.Client

**Package Version: 5.1.0** | **README Updated: 2026-03-31**

Complete .NET client for LogDB - the modern logging and observability platform.

> [!IMPORTANT]
> `LogPoint` and `LogRelation` tracks are marked **[Soon]** in the current public SDK build.
> Their write/read APIs are forward-compatible but currently throw `NotSupportedException`.

## Installation

```bash
dotnet add package LogDB.Client --source "https://nuget.pkg.github.com/logdbhq/index.json"
```

Add to your `nuget.config`:
```xml
<configuration>
  <packageSources>
    <add key="github" value="https://nuget.pkg.github.com/logdbhq/index.json" />
  </packageSources>
</configuration>
```

---

# Part 1: Sending Data (Writing)

## Quick Start - Fluent Builder (Recommended)

```csharp
using com.logdb.LogDB.LogBuilders;

// Set your API key once at startup
LogEventBuilder.ApiKey = "your-api-key";

// Optional: Set defaults
LogEventBuilder.Application = "MyApp";
LogEventBuilder.Environment = "Production";
LogEventBuilder.Collection = "logs";

// Send a log
await LogEventBuilder.Create()
    .SetMessage("User logged in successfully")
    .SetLogLevel(LogLevel.Info)
    .SetUserEmail("user@example.com")
    .AddLabel("authentication")
    .AddAttribute("userId", 12345)
    .AddAttribute("loginMethod", "OAuth")
    .Log();
```

## Payload Parameter Reference (Full Model Fields)

Source: `LogDB.Client/Models` (public properties only; internal client-managed fields omitted).

### `Log` (event record)

```csharp
public DateTime Timestamp { get; set; } = DateTime.UtcNow;
public string Application { get; set; } = string.Empty;
public string Environment { get; set; } = string.Empty;
public LogLevel Level { get; set; } = LogLevel.Info;
public string Message { get; set; } = string.Empty;
public string? Exception { get; set; }
public string? StackTrace { get; set; }
public string? Source { get; set; }
public int? UserId { get; set; }
public string? UserEmail { get; set; }
public string? CorrelationId { get; set; }
public string? RequestPath { get; set; }
public string? HttpMethod { get; set; }
public string? AdditionalData { get; set; }
public string? IpAddress { get; set; }
public int? StatusCode { get; set; }
public string? Description { get; set; }
public string Guid { get; set; } = System.Guid.NewGuid().ToString();
public string? Collection { get; set; }
public List<string> Label { get; init; } = new();
public Dictionary<string, string> AttributesS { get; init; } = new();
public Dictionary<string, double> AttributesN { get; init; } = new();
public Dictionary<string, bool> AttributesB { get; init; } = new();
public Dictionary<string, DateTime> AttributesD { get; init; } = new();
```

### `LogCache` (key-value state)

```csharp
public string Key { get; set; } = string.Empty;
public string Value { get; set; } = string.Empty;
public string Guid { get; set; } = System.Guid.NewGuid().ToString();
public DateTime? Timestamp { get; set; }
public string? Collection { get; set; }
public int? TtlSeconds { get; set; }
```

### `LogBeat` (heartbeat / health)

```csharp
public string Guid { get; set; } = System.Guid.NewGuid().ToString();
public string Measurement { get; set; } = string.Empty;
public List<LogMeta> Tag { get; set; } = new();
public List<LogMeta> Field { get; set; } = new();
public DateTime Timestamp { get; set; } = DateTime.UtcNow;
public string? Collection { get; set; }

// Convenience wrappers backed by Tag[]:
public string? Environment { get; set; } // writes tag: environment=<value>
public string? Application { get; set; } // writes tag: application=<value>
```

### `LogPoint` (metrics sample) [Soon]

> [!NOTE]
> Public SDK status: `[Soon]` - write/read methods are exposed for forward compatibility but currently throw `NotSupportedException`.

```csharp
public string Guid { get; set; } = System.Guid.NewGuid().ToString();
public string Measurement { get; set; } = string.Empty;
public List<LogMeta> Tag { get; set; } = new();
public List<LogMeta> Field { get; set; } = new();
public DateTime Timestamp { get; set; } = DateTime.UtcNow;
public string? Collection { get; set; }

// Convenience wrappers backed by Tag[]:
public string? Environment { get; set; } // writes tag: environment=<value>
public string? Application { get; set; } // writes tag: application=<value>
```

### `LogRelation` (graph edge) [Soon]

> [!NOTE]
> Public SDK status: `[Soon]` - write/read methods are exposed for forward compatibility but currently throw `NotSupportedException`.

```csharp
public string Origin { get; set; } = string.Empty;
public string Subject { get; set; } = string.Empty;
public string Relation { get; set; } = string.Empty;
public string Guid { get; set; } = System.Guid.NewGuid().ToString();
public DateTime? DateIn { get; set; }
public string? Collection { get; set; }
public string? Environment { get; set; }
public string? Application { get; set; }
public Dictionary<string, object>? OriginProperties { get; set; }
public Dictionary<string, object>? SubjectProperties { get; set; }
public Dictionary<string, object>? RelationProperties { get; set; }
```

### Shared helper types

```csharp
public class LogMeta
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

public enum LogLevel
{
    Trace = 0,
    Debug = 1,
    Info = 2,
    Warning = 3,
    Error = 4,
    Critical = 5,
    Exception = 6
}
```

## Sending Different Data Types

### 1. Regular Logs

```csharp
// Simple log
await LogEventBuilder.Create()
    .SetMessage("Order placed")
    .SetLogLevel(LogLevel.Info)
    .Log();

// Detailed log with context
await LogEventBuilder.Create()
    .SetMessage("Payment processed")
    .SetLogLevel(LogLevel.Info)
    .SetApplication("PaymentService")
    .SetEnvironment("Production")
    .SetCorrelationId("order-123-456")
    .SetUserEmail("customer@example.com")
    .SetIpAddress("192.168.1.100")
    .SetHttpMethod("POST")
    .SetRequestPath("/api/payments")
    .SetStatusCode(200)
    .AddLabel("payment")
    .AddLabel("success")
    .AddAttribute("orderId", "ORD-123")
    .AddAttribute("amount", 99.99)
    .AddAttribute("currency", "USD")
    .Log();
```

### 2. Exceptions

```csharp
try
{
    // Your code
}
catch (Exception ex)
{
    await LogEventBuilder.Create()
        .SetException(ex)  // Automatically sets level to Exception
        .SetSource("PaymentProcessor.Process")
        .SetCorrelationId(correlationId)
        .AddAttribute("orderId", orderId)
        .Log();
}
```

### 3. Metrics (LogPoints) [Soon]

> [!NOTE]
> This API group is currently disabled in public builds and throws `NotSupportedException`.

```csharp
using LogDB.Client.Models;

var logger = new Logger("your-api-key");

await logger.Log(new LogPoint
{
    Measurement = "http_request_duration",
    Collection = "metrics",
    Tag = new List<LogMeta>
    {
        new() { Key = "endpoint", Value = "/api/users" },
        new() { Key = "method", Value = "GET" },
        new() { Key = "status", Value = "200" }
    },
    Field = new List<LogMeta>
    {
        new() { Key = "duration_ms", Value = "125" },
        new() { Key = "bytes", Value = "4096" }
    }
});
```

### 4. Cache (Key-Value Storage)

```csharp
using LogDB.Client.Models;

await logger.Log(new LogCache
{
    Key = "session:user123",
    Value = "{\"userId\": 123, \"role\": \"admin\", \"expires\": \"2024-12-31\"}",
    Collection = "sessions",
    TtlSeconds = 3600 // 1 hour TTL
});
```

### 5. Relations (Graph Data) [Soon]

> [!NOTE]
> This API group is currently disabled in public builds and throws `NotSupportedException`.

```csharp
using LogDB.Client.Models;

await logger.Log(new LogRelation
{
    Origin = "user:123",
    Relation = "purchased",
    Subject = "product:456",
    Collection = "orders",
    OriginProperties = new Dictionary<string, object>
    {
        { "name", "John Doe" },
        { "tier", "premium" }
    },
    SubjectProperties = new Dictionary<string, object>
    {
        { "name", "Widget Pro" },
        { "category", "electronics" }
    },
    RelationProperties = new Dictionary<string, object>
    {
        { "order_id", "ORD-789" },
        { "quantity", "2" }
    }
});
```

### 6. Heartbeats (LogBeat)

```csharp
using LogDB.Client.Models;

await logger.Log(new LogBeat
{
    Measurement = "service_health",
    Collection = "heartbeats",
    Tag = new List<LogMeta>
    {
        new() { Key = "service", Value = "api-gateway" },
        new() { Key = "instance", Value = "prod-1" }
    },
    Field = new List<LogMeta>
    {
        new() { Key = "cpu_percent", Value = "45.2" },
        new() { Key = "memory_mb", Value = "1024" },
        new() { Key = "connections", Value = "150" }
    }
});
```

## Encryption Support

LogDB supports field-level encryption for sensitive data:

```csharp
// Encrypt individual fields
await LogEventBuilder.Create()
    .SetMessage("User data updated")
    .SetUserEmail("user@example.com", isEncrypted: true)
    .SetIpAddress("192.168.1.1", isEncrypted: true)
    .AddAttribute("ssn", "123-45-6789", isEncrypted: true)
    .Log();

// Or encrypt everything
await LogEventBuilder.Create()
    .SetMessage("Sensitive operation")
    .Encrypt()  // Encrypts all fields
    .Log();
```

---

# Part 2: Reading Data (Querying)

## Quick Start

```csharp
using LogDB.Extensions.Logging;

// Create reader
var reader = LogDBReaderExtensions.CreateReader("your-api-key");

// Query logs
var errors = await reader.QueryLogs()
    .FromApplication("MyApp")
    .WithLevel("Error")
    .InLastHours(24)
    .Take(100)
    .ExecuteAsync();

foreach (var log in errors.Items)
{
    Console.WriteLine($"[{log.Timestamp}] {log.Message}");
}
```

Reader endpoint discovery is built into the SDK. If you need to fetch the resolved grpc-server URL explicitly (for diagnostics or pinned startup config), use:

```csharp
using LogDB.Extensions.Logging;

var readerServiceUrl = await LogDBReaderExtensions.DiscoverReaderServiceUrlAsync();
// or sync: var readerServiceUrl = LogDBReaderExtensions.DiscoverReaderServiceUrl();

var reader = LogDBReaderExtensions.CreateReader(
    apiKey: "your-api-key",
    readerServiceUrl: readerServiceUrl);
```

Environment override:
- `LOGDB_GRPC_SERVER_URL` forces a specific reader endpoint.

## Fluent Query API

### Querying Logs

```csharp
// Get recent errors
var errors = await reader.QueryLogs()
    .FromApplication("MyApp")
    .InEnvironment("Production")
    .WithLevel("Error")
    .InLastHours(4)
    .Take(50)
    .ExecuteAsync();

// Search by text
var results = await reader.QueryLogs()
    .Containing("timeout")
    .OnlyExceptions()
    .InLastDays(7)
    .ExecuteAsync();

// Filter by user and correlation
var userLogs = await reader.QueryLogs()
    .ByUser("user@example.com")
    .WithCorrelationId("order-123")
    .ExecuteAsync();

// Filter by HTTP context
var apiErrors = await reader.QueryLogs()
    .WithHttpMethod("POST")
    .WithRequestPath("/api/payments")
    .WithStatusCode(500)
    .InLastHours(1)
    .ExecuteAsync();

// Filter by labels and attributes
var criticalLogs = await reader.QueryLogs()
    .WithLabel("critical")
    .WithLabels("production", "payment")
    .WithAttribute("region", "us-east-1")
    .ExecuteAsync();

// Pagination and sorting
var page2 = await reader.QueryLogs()
    .FromApplication("MyApp")
    .OrderByTimestamp(ascending: false)
    .Skip(50)
    .Take(50)
    .ExecuteAsync();

// Get first match
var latestError = await reader.QueryLogs()
    .WithLevel("Error")
    .OrderByTimestamp(ascending: false)
    .FirstOrDefaultAsync();

// Count logs
var errorCount = await reader.QueryLogs()
    .WithLevel("Error")
    .InLastHours(24)
    .CountAsync();
```

### Querying Cache

```csharp
// Get by key
var session = await reader.QueryCache()
    .GetAsync("session:user123");

// Search by pattern
var allSessions = await reader.QueryCache()
    .WithKeyPattern("session:*")
    .InLastHours(1)
    .ExecuteAsync();

// Query cache in collection
var configs = await reader.QueryCache()
    .InCollection("config")
    .Take(100)
    .ExecuteAsync();
```

### Querying Metrics (LogPoints) [Soon]

> [!NOTE]
> This API group is currently disabled in public builds and throws `NotSupportedException`.

```csharp
// Get metrics by measurement
var cpuMetrics = await reader.QueryLogPoints()
    .ForMeasurement("cpu_usage")
    .InLastHours(4)
    .ExecuteAsync();

// Filter by tags
var apiMetrics = await reader.QueryLogPoints()
    .ForMeasurement("http_request_duration")
    .WithTag("endpoint", "/api/users")
    .WithTag("method", "GET")
    .InLastMinutes(30)
    .ExecuteAsync();

// Get available measurements
var measurements = await reader.GetLogPointMeasurementsAsync();
```

### Querying Relations [Soon]

> [!NOTE]
> This API group is currently disabled in public builds and throws `NotSupportedException`.

```csharp
// Find relations by origin
var purchases = await reader.QueryRelations()
    .FromOrigin("user:123")
    .WithRelation("purchased")
    .InLastDays(30)
    .ExecuteAsync();

// Find relations by subject
var buyers = await reader.QueryRelations()
    .ToSubject("product:456")
    .ExecuteAsync();

// Graph traversal - find related entities
var related = await reader.QueryRelations()
    .GetRelatedAsync(
        entity: "order:789",
        direction: "both",  // "incoming", "outgoing", or "both"
        depth: 2
    );
```

### Statistics and Time Series

```csharp
// Get log statistics
var stats = await reader.GetLogStatsAsync(new LogStatsParams
{
    FromDate = DateTime.UtcNow.AddDays(-7),
    ToDate = DateTime.UtcNow,
    GroupBy = "level"
});

Console.WriteLine($"Total: {stats.TotalCount}");
Console.WriteLine($"Errors: {stats.ErrorCount}");
Console.WriteLine($"Warnings: {stats.WarningCount}");

// Get time series data
var timeSeries = await reader.GetLogTimeSeriesAsync(new LogTimeSeriesParams
{
    FromDate = DateTime.UtcNow.AddDays(-1),
    Interval = "hour",
    GroupBy = "level"
});

foreach (var point in timeSeries)
{
    Console.WriteLine($"{point.Timestamp}: {point.Count} ({point.Group})");
}
```

### Direct Query Methods

```csharp
// Get log by ID
var log = await reader.GetLogByIdAsync("123");

// Get log by GUID
var log = await reader.GetLogByGuidAsync("550e8400-e29b-41d4-a716-446655440000");

// Get distinct values
var applications = await reader.GetDistinctValuesAsync("Application");
var levels = await reader.GetDistinctValuesAsync("Level");
var environments = await reader.GetDistinctValuesAsync("Environment");
```

---

# Dependency Injection

## ASP.NET Core Setup

```csharp
// Program.cs
builder.Services.AddLogDBClient(options =>
{
    options.ApiKey = "your-api-key";
    options.DefaultApplication = "MyApp";
    options.DefaultEnvironment = "Production";
    options.EnableBatching = true;
    options.BatchSize = 100;
    options.FlushInterval = TimeSpan.FromSeconds(5);
});

builder.Services.AddLogDBReader("your-api-key");
```

## Using in Services

```csharp
public class OrderService
{
    private readonly ILogDBClient _writer;
    private readonly ILogDBReader _reader;

    public OrderService(ILogDBClient writer, ILogDBReader reader)
    {
        _writer = writer;
        _reader = reader;
    }

    public async Task ProcessOrder(Order order)
    {
        // Write a log
        await _writer.LogAsync(new Log
        {
            Message = $"Processing order {order.Id}",
            Level = "Info",
            Application = "OrderService"
        });

        // Query related logs
        var previousLogs = await _reader.QueryLogs()
            .WithCorrelationId(order.CorrelationId)
            .ExecuteAsync();
    }
}
```

## Microsoft.Extensions.Logging Integration

```csharp
// Program.cs
builder.Logging.AddLogDB(options =>
{
    options.ApiKey = "your-api-key";
    options.DefaultApplication = "MyApp";
    options.MinimumLevel = LogLevel.Information;
});

// In your code - use standard ILogger
public class MyController
{
    private readonly ILogger<MyController> _logger;

    public MyController(ILogger<MyController> logger)
    {
        _logger = logger;
    }

    public IActionResult Index()
    {
        _logger.LogInformation("Page accessed");  // Sent to LogDB!
        return View();
    }
}
```

## OpenTelemetry Integration

```csharp
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;

var resourceBuilder = ResourceBuilder.CreateDefault()
    .AddService("MyApp", serviceVersion: "1.0.0")
    .AddAttributes(new[]
    {
        new KeyValuePair<string, object>("deployment.environment", "production")
    });

builder.Logging.AddOpenTelemetry(logging =>
{
    logging.IncludeScopes = true;
    logging.ParseStateValues = true;
    logging.AddLogDBExporter(resourceBuilder, options =>
    {
        options.ApiKey = "your-api-key";
        options.Endpoint = "https://your-grpc-logger-endpoint";
        options.DefaultCollection = "logs";
    });
});

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddLogDBExporter(resourceBuilder, options =>
    {
        options.ApiKey = "your-api-key";
        options.DefaultCollection = "traces";
    }))
    .WithMetrics(metrics => metrics.AddLogDBExporter(resourceBuilder, options =>
    {
        options.ApiKey = "your-api-key";
        options.DefaultCollection = "metrics";
        options.MetricExportIntervalMilliseconds = 30000;
        options.MetricTemporalityPreference = MetricReaderTemporalityPreference.Cumulative;
    }));
```

### OpenTelemetry Identity Resolution

Service/Application is resolved in this order:
1. OpenTelemetry Resource `service.name`
2. `LogDBExporterOptions.ServiceName`
3. `LOGDB_DEFAULT_APPLICATION` / `OTEL_SERVICE_NAME`
4. Entry assembly name (fallback)

Environment is resolved in this order:
1. OpenTelemetry Resource `deployment.environment`
2. OpenTelemetry Resource `deployment.environment.name`
3. `LogDBExporterOptions.DefaultEnvironment`
4. `production`

### OpenTelemetry Protocol Expectations

- `Protocol = LogDBProtocol.Native` (default): `Endpoint`/`ServiceUrl` should point to LogDB writer gRPC endpoint.
- `Protocol = LogDBProtocol.OpenTelemetry`: `Endpoint`/`ServiceUrl` should point to an OTLP receiver or collector endpoint.
- `ApiKey` is required for all three OTEL exporters (logs, traces, metrics).
- Additional gateway/auth headers can be set through `options.Headers`, for example:

```csharp
options.Headers["X-Forwarded-Api-Key"] = "gateway-key";
options.Headers["X-Tenant"] = "customer-a";
```

### OpenTelemetry Exporter Options

| Option | Default | Notes |
|--------|---------|-------|
| `ApiKey` | required | Required for logs/traces/metrics exporters |
| `Endpoint` / `ServiceUrl` | auto-discovery | `ServiceUrl` is an alias for `Endpoint` |
| `ServiceName` | resolved | Used when `service.name` is missing in Resource |
| `DefaultEnvironment` | `production` fallback | Used when deployment environment keys are missing |
| `DefaultCollection` | `logs`/`traces`/`metrics` | Exporter-specific default if not set |
| `ExporterTimeoutMilliseconds` | 30000 | Export timeout for synchronous exporter path |
| `MetricExportIntervalMilliseconds` | 60000 | Periodic metrics export interval |
| `MetricExportTimeoutMilliseconds` | 30000 | Periodic metrics export timeout |
| `MetricTemporalityPreference` | provider default | Applied through `MetricReaderOptions` when supported by host pipeline |
| `Headers` | empty | Extra request headers for gateways/proxies |

---

# Configuration Reference

| Option | Default | Description |
|--------|---------|-------------|
| `ApiKey` | (required) | Your LogDB API key |
| `ServiceUrl` | auto-discover | Writer service URL |
| `ReaderServiceUrl` | auto-discover | Reader service URL |
| `DefaultApplication` | null | Default application name |
| `DefaultEnvironment` | "production" | Default environment |
| `DefaultCollection` | "logs" | Default collection |
| `EnableBatching` | true | Batch log entries |
| `BatchSize` | 100 | Entries per batch |
| `FlushInterval` | 5 seconds | Max wait before flush |
| `EnableCompression` | true | Compress payloads |
| `MaxRetries` | 3 | Retry failed requests |
| `EnableCircuitBreaker` | true | Circuit breaker pattern |
| `EnableSampling` | false | Enable log sampling |

---

# Features Summary

| Feature | Write | Read |
|---------|-------|------|
| **Logs** | Yes - `LogEventBuilder` / `ILogDBClient` | Yes - `QueryLogs()` |
| **Metrics** | **Soon** - `LogPoint` | **Soon** - `QueryLogPoints()` |
| **Cache** | Yes - `LogCache` | Yes - `QueryCache()` |
| **Relations** | **Soon** - `LogRelation` | **Soon** - `QueryRelations()` |
| **Heartbeats** | Yes - `LogBeat` | Yes - `GetLogBeatsAsync()` |
| **Statistics** | - | Yes - `GetLogStatsAsync()` |
| **Time Series** | - | Yes - `GetLogTimeSeriesAsync()` |
| **Graph Traversal** | - | Yes - `GetRelatedEntitiesAsync()` |

---

# License

MIT License - see LICENSE file for details.


