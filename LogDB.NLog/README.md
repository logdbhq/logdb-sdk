# LogDB.NLog

**Package Version: 5.1.1** | **README Updated: 2026-03-31**

NLog target for LogDB - Send your NLog logs directly to LogDB with full structured logging support, filtering, and batching.

## Installation

```bash
dotnet add package LogDB.NLog
```

## Quick Start

### XML Configuration

```xml
<nlog>
  <extensions>
    <add assembly="LogDB.NLog" />
  </extensions>

  <targets>
    <target name="logdb"
            xsi:type="LogDB"
            apiKey="your-api-key"
            defaultApplication="MyApp"
            defaultEnvironment="Production"
            defaultPayloadType="Log" />
  </targets>

  <rules>
    <logger name="*" minLevel="Info" writeTo="logdb" />
  </rules>
</nlog>
```

### Code-Based Configuration

```csharp
using NLog;
using NLog.Config;
using LogDB.NLog;

var config = new LoggingConfiguration();

var logDBTarget = new LogDBTarget
{
    ApiKey = "your-api-key",
    DefaultApplication = "MyApp",
    DefaultEnvironment = "Production",
    DefaultPayloadType = LogDBPayloadType.Log
};

config.AddTarget("logdb", logDBTarget);
config.AddRule(LogLevel.Info, LogLevel.Fatal, "logdb");

LogManager.Configuration = config;

var logger = LogManager.GetCurrentClassLogger();
logger.Info("Hello from NLog to LogDB!");
```

Tenant scope is resolved from the API key on the server side.

## Basic Usage

### Simple Configuration

```csharp
var config = new LoggingConfiguration();
var logDBTarget = new LogDBTarget
{
    ApiKey = "your-api-key",
    DefaultPayloadType = LogDBPayloadType.Log
};
config.AddTarget("logdb", logDBTarget);
config.AddRule(LogLevel.Info, LogLevel.Fatal, "logdb");
LogManager.Configuration = config;
```

### Advanced Configuration

```csharp
var config = new LoggingConfiguration();

var logDBTarget = new LogDBTarget
{
    ApiKey = "your-api-key",
    DefaultApplication = "MyApp",
    DefaultEnvironment = "Production",
    DefaultCollection = "app-logs",
    DefaultPayloadType = LogDBPayloadType.Log,
    MinimumLevel = LogLevel.Info,

    // Batching options
    EnableBatching = true,
    BatchSize = 100,
    FlushIntervalSeconds = 5,

    // Reliability options
    MaxRetries = 3,
    EnableCircuitBreaker = true,
    EnableCompression = true,

    // Custom filter
    Filter = logEvent =>
        logEvent.Level >= LogLevel.Warning ||
        logEvent.Properties.ContainsKey("Important")
};

config.AddTarget("logdb", logDBTarget);
config.AddRule(LogLevel.Info, LogLevel.Fatal, "logdb");
config.AddRule(LogLevel.Warn, LogLevel.Fatal, "logdb", "Microsoft.*");

LogManager.Configuration = config;
```

## Cache + Beat Routing (Event Properties)

Payload type is mandatory.
Set a global default payload type on the target, then override per-event when needed.

Control properties:
- `LogDBType`: `LogDBPayloadType.Log` | `LogDBPayloadType.Cache` | `LogDBPayloadType.Beat`
- Shared: `LogDBCollection`

Cache properties:
- `LogDBCacheKey` (required)
- `LogDBCacheValue` (optional, falls back to log message)
- `LogDBTtlSeconds` (optional)

Beat properties:
- `LogDBMeasurement` (required)
- `LogDBApplication` (optional)
- `LogDBEnvironment` (optional)
- `LogDBTags` (optional dictionary)
- `LogDBFields` (optional dictionary)
- Prefix alternatives: `Tag.<key>`, `Field.<key>`

```csharp
// Global default payload type for regular logs
var logDBTarget = new LogDBTarget
{
    ApiKey = "your-api-key",
    DefaultPayloadType = LogDBPayloadType.Log
};

// Cache write through NLog
var cacheEvent = new LogEventInfo(LogLevel.Info, "Demo", "cache upsert");
cacheEvent.Properties["LogDBType"] = LogDBPayloadType.Cache;
cacheEvent.Properties["LogDBCacheKey"] = "session:user-42";
cacheEvent.Properties["LogDBCacheValue"] = "active";
cacheEvent.Properties["LogDBTtlSeconds"] = 600;
logger.Log(cacheEvent);

// Beat write through NLog
var beatEvent = new LogEventInfo(LogLevel.Info, "Demo", "heartbeat");
beatEvent.Properties["LogDBType"] = LogDBPayloadType.Beat;
beatEvent.Properties["LogDBMeasurement"] = "worker_health";
beatEvent.Properties["Tag.worker"] = "worker-1";
beatEvent.Properties["Field.status"] = "ok";
beatEvent.Properties["Field.cpu"] = 23.4;
logger.Log(beatEvent);
```

## Structured Logging

All NLog properties are automatically mapped to LogDB attributes:

```csharp
var logEvent = new LogEventInfo(LogLevel.Info, "MyLogger", "User {UserId} logged in from {IpAddress}");
logEvent.Properties["UserId"] = 12345;
logEvent.Properties["IpAddress"] = "192.168.1.1";
logger.Log(logEvent);

// Maps to:
// - AttributesS["UserId"] = "12345" (or AttributesN if numeric)
// - AttributesS["IpAddress"] = "192.168.1.1"
```

### Property Type Mapping

- **Strings** -> `AttributesS`
- **Numbers** (int, long, double, etc.) -> `AttributesN`
- **Booleans** -> `AttributesB`
- **DateTime/DateTimeOffset** -> `AttributesD`
- **Complex objects** -> JSON string in `AttributesS`
- **Collections** -> JSON string in `AttributesS`

## Exception Handling

Exceptions are automatically captured with full stack traces:

```csharp
try
{
    // Your code
}
catch (Exception ex)
{
    var logEvent = new LogEventInfo(LogLevel.Error, "MyLogger", "An error occurred");
    logEvent.Exception = ex;
    logger.Log(logEvent);

    // Automatically maps:
    // - Exception = exception type name
    // - StackTrace = full stack trace
    // - AttributesS["ExceptionMessage"] = exception message
}
```

## Correlation and Tracing

The target automatically extracts correlation IDs and trace information:

```csharp
// Using properties
var logEvent = new LogEventInfo(LogLevel.Info, "MyLogger", "Processing request");
logEvent.Properties["CorrelationId"] = correlationId;
logEvent.Properties["TraceId"] = traceId;
logEvent.Properties["SpanId"] = spanId;
logger.Log(logEvent);
// Maps to Log.CorrelationId and AttributesS

// Activity/Trace support
using var activity = Activity.StartActivity("ProcessOrder");
var activityEvent = new LogEventInfo(LogLevel.Info, "MyLogger", "Order processed");
// Automatically extracts TraceId, SpanId, ParentSpanId from Activity.Current
logger.Log(activityEvent);
```

## HTTP Context

Extract HTTP context information:

```csharp
var logEvent = new LogEventInfo(LogLevel.Info, "MyLogger", "Request completed");
logEvent.Properties["RequestPath"] = "/api/orders";
logEvent.Properties["HttpMethod"] = "POST";
logEvent.Properties["StatusCode"] = 200;
logEvent.Properties["IpAddress"] = "192.168.1.1";
logEvent.Properties["UserId"] = 12345;
logEvent.Properties["UserEmail"] = "user@example.com";
logger.Log(logEvent);

// Maps to:
// - RequestPath
// - HttpMethod
// - StatusCode
// - IpAddress
// - UserId
// - UserEmail
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
| `MinimumLevel` | `Info` | Minimum log level to send |
| `EnableBatching` | `true` | Enable log batching |
| `BatchSize` | `100` | Number of logs per batch |
| `FlushIntervalSeconds` | `5` | Maximum wait before flushing (seconds) |
| `EnableCompression` | `true` | Compress payloads |
| `MaxRetries` | `3` | Maximum retry attempts |
| `EnableCircuitBreaker` | `true` | Enable circuit breaker |
| `Filter` | null | Custom log filter function |
| `FallbackTarget` | null | Fallback target if LogDB fails |

Tenant scope is resolved from the provided API key automatically.

## XML Configuration

### Basic XML Config

```xml
<nlog>
  <extensions>
    <add assembly="LogDB.NLog" />
  </extensions>

  <targets>
    <target name="logdb"
            xsi:type="LogDB"
            apiKey="${logdb:apikey}"
            defaultApplication="MyApp"
            defaultEnvironment="${environment}"
            defaultCollection="app-logs"
            defaultPayloadType="Log"
            minimumLevel="Info"
            enableBatching="true"
            batchSize="100"
            flushIntervalSeconds="5" />
  </targets>

  <rules>
    <logger name="Microsoft.*" minLevel="Warning" writeTo="logdb" />
    <logger name="*" minLevel="Info" writeTo="logdb" />
  </rules>
</nlog>
```

### Advanced XML Config with Filtering

```xml
<nlog>
  <extensions>
    <add assembly="LogDB.NLog" />
  </extensions>

  <targets>
    <target name="console" xsi:type="Console" />

    <target name="logdb"
            xsi:type="LogDB"
            apiKey="${logdb:apikey}"
            defaultApplication="MyApp"
            defaultPayloadType="Log"
            enableBatching="true"
            batchSize="100"
            maxRetries="3"
            enableCircuitBreaker="true"
            enableCompression="true" />
  </targets>

  <rules>
    <!-- Send only warnings and errors from Microsoft.* -->
    <logger name="Microsoft.*" minLevel="Warning" writeTo="logdb" />

    <!-- Send all Info and above from other loggers -->
    <logger name="*" minLevel="Info" writeTo="logdb" />
  </rules>
</nlog>
```

## Custom Filtering

### Programmatic Filter

```csharp
var logDBTarget = new LogDBTarget
{
    ApiKey = "your-api-key",
    DefaultPayloadType = LogDBPayloadType.Log,
    Filter = logEvent =>
    {
        // Only send logs with specific properties
        return logEvent.Properties.ContainsKey("Important") ||
               logEvent.Level >= LogLevel.Warning;
    }
};
```

### XML-Based Filtering

NLog's built-in rule system provides powerful filtering:

```xml
<rules>
  <!-- Only send errors from specific namespace -->
  <logger name="MyApp.Services.*" minLevel="Error" writeTo="logdb" />

  <!-- Send warnings and above from all other loggers -->
  <logger name="*" minLevel="Warn" writeTo="logdb" />
</rules>
```

## Integration with ASP.NET Core

```csharp
// Program.cs
using NLog;
using NLog.Web;

var builder = WebApplication.CreateBuilder(args);

// Configure NLog
LogManager.Configuration = new NLogLoggingConfiguration(builder.Configuration.GetSection("NLog"));

var app = builder.Build();

// Use ILogger<T> in your controllers/services
app.MapGet("/", (ILogger<Program> logger) => {
    logger.LogInformation("Hello from ASP.NET Core!");
    return "Hello World";
});

app.Run();
```

```xml
<!-- NLog.config -->
<nlog>
  <extensions>
    <add assembly="LogDB.NLog" />
  </extensions>

  <targets>
    <target name="logdb"
            xsi:type="LogDB"
            apiKey="${configsetting:item=LogDB:ApiKey}"
            defaultApplication="MyWebApp"
            defaultPayloadType="Log"
            defaultEnvironment="${configsetting:item=ASPNETCORE_ENVIRONMENT}" />
  </targets>

  <rules>
    <logger name="Microsoft.*" minLevel="Warning" writeTo="logdb" />
    <logger name="*" minLevel="Info" writeTo="logdb" />
  </rules>
</nlog>
```

## Performance

- **Batching**: Logs are automatically batched for efficient transmission
- **Async**: All log sending is asynchronous and non-blocking
- **Circuit Breaker**: Automatic protection against service failures
- **Compression**: Payloads are compressed to reduce bandwidth

## Error Handling

The target handles errors gracefully:

```csharp
var logDBTarget = new LogDBTarget
{
    ApiKey = "your-api-key",
    DefaultPayloadType = LogDBPayloadType.Log,
    FallbackTarget = new FileTarget("fallback")
    {
        FileName = "logs/fallback.log"
    }
};

// If LogDB fails, logs will be written to fallback target
```

## Examples

### Multiple Targets

```xml
<nlog>
  <extensions>
    <add assembly="LogDB.NLog" />
  </extensions>

  <targets>
    <target name="console" xsi:type="Console" />
    <target name="file" xsi:type="File" fileName="logs/app.log" />
    <target name="logdb"
            xsi:type="LogDB"
            apiKey="your-api-key" />
  </targets>

  <rules>
    <logger name="*" minLevel="Info" writeTo="console,file,logdb" />
  </rules>
</nlog>
```

### Using Extension Methods

```csharp
using LogDB.NLog;

var config = new LoggingConfiguration();
config.AddLogDBTargetWithRule(
    "logdb",
    "your-api-key",
    LogDBPayloadType.Log,
    LogLevel.Info,
    LogLevel.Fatal,
    target => {
        target.DefaultApplication = "MyApp";
        target.DefaultEnvironment = "Production";
    }
);

LogManager.Configuration = config;
```

## Property Type Mapping

NLog properties are automatically mapped to LogDB attribute types:

- **Strings** -> `AttributesS`
- **Numbers** (int, long, double, etc.) -> `AttributesN`
- **Booleans** -> `AttributesB`
- **DateTime/DateTimeOffset** -> `AttributesD`
- **Complex objects** -> JSON string in `AttributesS`
- **Collections** -> JSON string in `AttributesS`

## Comparison with Serilog

| Feature | Serilog Sink | NLog Target |
|---------|--------------|-------------|
| Configuration | Fluent API | XML or Code-based |
| Filtering | Custom function | Rules + Custom function |
| Property Model | Typed (`LogEventPropertyValue`) | Boxed (`object`) |
| Async | Built-in | `WriteAsyncTask()` |
| Batching | Via client | Via client |
| Retry/Circuit Breaker | Via client | Via client |

Both implementations use the same underlying `LogDBClient` for sending logs, ensuring consistent behavior.

## License

MIT License - see LICENSE file for details.


