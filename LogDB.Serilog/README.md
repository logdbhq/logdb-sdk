# LogDB.Serilog

**Package Version: 5.1.1** | **README Updated: 2026-03-31**

Serilog sink for LogDB - Send your Serilog logs directly to LogDB with full structured logging support.

## Installation

```bash
dotnet add package LogDB.Serilog
```

## Quick Start

```csharp
using Serilog;
using LogDB.Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.LogDB(options => {
        options.ApiKey = "your-api-key";
        options.DefaultApplication = "MyApp";
        options.DefaultEnvironment = "Production";
        options.DefaultPayloadType = LogDBPayloadType.Log;
    })
    .CreateLogger();

Log.Information("Hello from Serilog to LogDB!");
```

Tenant scope is resolved from the API key on the server side.

## Basic Usage

### Simple Configuration

```csharp
Log.Logger = new LoggerConfiguration()
    .WriteTo.LogDB("your-api-key", LogDBPayloadType.Log)
    .CreateLogger();
```

## Payload Routing (Structured Properties)

Payload type is mandatory.
Set a global default payload type in sink options, then override per-event when needed.

Control properties:
- `LogDBType`: `LogDBPayloadType.Log` | `LogDBPayloadType.Cache` | `LogDBPayloadType.Beat`
- Shared: `LogDBCollection`

Cache properties:
- `LogDBCacheKey` (required)
- `LogDBCacheValue` (optional, falls back to rendered message)
- `LogDBTtlSeconds` (optional)

Beat properties:
- `LogDBMeasurement` (required)
- `LogDBApplication` (optional)
- `LogDBEnvironment` (optional)
- `LogDBTags` (optional dictionary/object)
- `LogDBFields` (optional dictionary/object)
- Prefix alternatives: `Tag.<key>`, `Field.<key>`

```csharp
// Global default payload type for regular logs
Log.Logger = new LoggerConfiguration()
   .WriteTo.LogDB(options =>
   {
      options.ApiKey = "your-api-key";
      options.DefaultPayloadType = LogDBPayloadType.Log;
   })
   .CreateLogger();

// Cache write through Serilog
Log.ForContext("LogDBType", LogDBPayloadType.Cache)
   .ForContext("LogDBCacheKey", "session:user-42")
   .ForContext("LogDBCacheValue", "active")
   .ForContext("LogDBTtlSeconds", 600)
   .Information("cache upsert");

// Beat write through Serilog
Log.ForContext("LogDBType", LogDBPayloadType.Beat)
   .ForContext("LogDBMeasurement", "worker_health")
   .ForContext("Tag.worker", "worker-1")
   .ForContext("Field.status", "ok")
   .ForContext("Field.cpu", 23.4)
   .Information("heartbeat");
```

### Advanced Configuration

```csharp
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithThreadId()
    .WriteTo.Console()
    .WriteTo.LogDB(options => {
        options.ApiKey = "your-api-key";
        options.DefaultApplication = "MyApp";
        options.DefaultEnvironment = "Production";
        options.DefaultCollection = "app-logs";
        options.DefaultPayloadType = LogDBPayloadType.Log;
        options.RestrictedToMinimumLevel = LogEventLevel.Information;

        // Batching options
        options.EnableBatching = true;
        options.BatchSize = 100;
        options.FlushInterval = TimeSpan.FromSeconds(5);

        // Reliability options
        options.MaxRetries = 3;
        options.EnableCircuitBreaker = true;
        options.EnableCompression = true;

        // Error handling
        options.OnError = (ex, logEvent) => {
            Console.WriteLine($"Failed to send log: {ex.Message}");
        };
    })
    .CreateLogger();
```

## Structured Logging

All Serilog structured properties are automatically mapped to LogDB attributes:

```csharp
Log.Information("User {UserId} logged in from {IpAddress}", userId, ipAddress);
// Maps to:
// - AttributesS["UserId"] = userId (as string)
// - AttributesS["IpAddress"] = ipAddress

Log.Information("Order {OrderId} total: {Total:C}", orderId, total);
// Maps to:
// - AttributesS["OrderId"] = orderId
// - AttributesN["Total"] = total (as number)
```

## Exception Handling

Exceptions are automatically captured with full stack traces:

```csharp
try
{
    // Your code
}
catch (Exception ex)
{
    Log.Error(ex, "An error occurred while processing order {OrderId}", orderId);
    // Automatically maps:
    // - Exception = exception type
    // - StackTrace = full stack trace
    // - AttributesS["ExceptionMessage"] = exception message
}
```

## Correlation and Tracing

The sink automatically extracts correlation IDs and trace information:

```csharp
// Using Serilog enrichers
LogContext.PushProperty("CorrelationId", correlationId);
Log.Information("Processing request");
// Maps to Log.CorrelationId

// Activity/Trace support
using var activity = Activity.StartActivity("ProcessOrder");
Log.Information("Order processed");
// Automatically extracts TraceId, SpanId, ParentSpanId
```

## HTTP Context

Extract HTTP context information:

```csharp
LogContext.PushProperty("RequestPath", "/api/orders");
LogContext.PushProperty("HttpMethod", "POST");
LogContext.PushProperty("StatusCode", 200);
LogContext.PushProperty("IpAddress", "192.168.1.1");

Log.Information("Request completed");
// Maps to:
// - RequestPath
// - HttpMethod
// - StatusCode
// - IpAddress
```

## Configuration Options

| Option | Default | Description |
|--------|---------|-------------|
| `ApiKey` | (required) | LogDB API key for authentication |
| `ServiceUrl` | auto-discover | LogDB service URL |
| `DefaultApplication` | null | Default application name |
| `DefaultEnvironment` | "production" | Default environment |
| `DefaultCollection` | "logs" | Default collection name |
| `DefaultPayloadType` | null | Explicit default payload type when `LogDBType` is not set per-event |
| `RestrictedToMinimumLevel` | `Information` | Minimum log level to send |
| `EnableBatching` | `true` | Enable log batching |
| `BatchSize` | `100` | Number of logs per batch |
| `FlushInterval` | `5 seconds` | Maximum wait before flushing |
| `EnableCompression` | `true` | Compress payloads |
| `MaxRetries` | `3` | Maximum retry attempts |
| `EnableCircuitBreaker` | `true` | Enable circuit breaker |
| `FormatProvider` | null | Custom format provider |
| `Filter` | null | Custom log filter function |
| `OnError` | null | Error callback |

Tenant scope is resolved from the provided API key automatically.

## Integration with ASP.NET Core

```csharp
// Program.cs
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "MyWebApp")
    .WriteTo.Console()
    .WriteTo.LogDB(options => {
        options.ApiKey = builder.Configuration["LogDB:ApiKey"];
        options.DefaultApplication = "MyWebApp";
        options.DefaultEnvironment = builder.Environment.EnvironmentName;
        options.DefaultPayloadType = LogDBPayloadType.Log;
    })
    .CreateLogger();

builder.Host.UseSerilog();

var app = builder.Build();

// Use ILogger<T> in your controllers/services
app.MapGet("/", (ILogger<Program> logger) => {
    logger.LogInformation("Hello from ASP.NET Core!");
    return "Hello World";
});

app.Run();
```

## Property Type Mapping

Serilog properties are automatically mapped to LogDB attribute types:

- **Strings** -> `AttributesS`
- **Numbers** (int, long, double, etc.) -> `AttributesN`
- **Booleans** -> `AttributesB`
- **DateTime/DateTimeOffset** -> `AttributesD`
- **Complex objects** -> JSON string in `AttributesS`
- **Collections** -> JSON string in `AttributesS`

## Performance

- **Batching**: Logs are automatically batched for efficient transmission
- **Async**: All log sending is asynchronous and non-blocking
- **Circuit Breaker**: Automatic protection against service failures
- **Compression**: Payloads are compressed to reduce bandwidth

## Error Handling

The sink handles errors gracefully:

```csharp
options.OnError = (ex, logEvent) => {
    // Log to console, file, or another sink
    Console.WriteLine($"Failed to send log to LogDB: {ex.Message}");
    // The original log event is available for retry or fallback
};
```

## Examples

### Multiple Sinks

```csharp
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/app.log")
    .WriteTo.LogDB(options => {
        options.ApiKey = "your-api-key";
        options.DefaultPayloadType = LogDBPayloadType.Log;
    })
    .CreateLogger();
```

### Custom Filtering

```csharp
Log.Logger = new LoggerConfiguration()
    .WriteTo.LogDB(options => {
        options.ApiKey = "your-api-key";
        options.DefaultPayloadType = LogDBPayloadType.Log;
        options.Filter = logEvent => {
            // Only send logs with specific properties
            return logEvent.Properties.ContainsKey("Important");
        };
    })
    .CreateLogger();
```

### Custom Format Provider

```csharp
Log.Logger = new LoggerConfiguration()
    .WriteTo.LogDB(options => {
        options.ApiKey = "your-api-key";
        options.DefaultPayloadType = LogDBPayloadType.Log;
        options.FormatProvider = new CultureInfo("en-US");
    })
    .CreateLogger();
```

## License

MIT License - see LICENSE file for details.


