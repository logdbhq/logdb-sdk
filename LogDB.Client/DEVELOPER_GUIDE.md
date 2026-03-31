# LogDB .NET Client Developer Guide

This guide is intended for developers and AI agents integrating `com.logdb.nuget` (LogDB.Client) into .NET applications.

## Overview

The LogDB client provides a high-performance, resilient, and batched logging interface for the LogDB platform. It supports structured logs, metrics (LogPoints), caching, and graph relations.

## Installation

```xml
<PackageReference Include="LogDB.Client" Version="5.0.18" />
```

## Core Concepts

-   **`ILogDBClient`**: The primary, modern interface for interacting with LogDB. It is thread-safe, resilient (retries + circuit breakers), and supports batching.
-   **`Logger`**: A legacy static-friendly wrapper. In version 4.x+, this wraps `ILogDBClient` internally, so it is safe to use and ensures high performance.
-   **Fluent API**: A builder pattern (`LogEventBuilder`) for constructing complex log entries easily.

## Usage scenarios

### 1. Modern Dependency Injection (Preferred)

For ASP.NET Core or generic hosts, register the client in your `Startup.cs` or `Program.cs`.

```csharp
using LogDB.Extensions.Logging;

// In Service Configuration
builder.Services.AddLogDB(options =>
{
    options.ApiKey = "YOUR_API_KEY";
    options.ServiceUrl = "https://logdb-server:5001";
    options.DefaultCollection = "app-logs";
    
    // Performance Settings
    options.EnableBatching = true;
    options.BatchSize = 100;
    options.FlushInterval = TimeSpan.FromSeconds(5);
    
    // Resilience
    options.EnableCircuitBreaker = true;
});

// Usage in Controllers/Services
public class MyService
{
    private readonly ILogDBClient _logDb;

    public MyService(ILogDBClient logDb)
    {
        _logDb = logDb;
    }

    public async Task DoWork()
    {
        await _logDb.LogAsync(new Log 
        { 
            Message = "Work started", 
            Level = "Info" 
        });
    }
}
```

### 2. Legacy / Static Contexts

If DI is not available, you can use the `Logger` facade.

```csharp
using com.logdb.logger;

// Initialize once (thread-safe, singleton-like)
var logger = await Logger.CreateAsync("YOUR_API_KEY", options => 
{
    options.ServiceUrl = "https://logdb-server:5001";
});

// Fluent API Usage
await logger.Event()
    .SetMessage("User logged in")
    .SetLevel(LogLevel.Information)
    .SetUser("user@example.com")
    .AddAttribute("ip_address", "127.0.0.1")
    .SendAsync();
```

## Data Types

### Logs
Standard structured logging.
```csharp
var log = new Log 
{
    Message = "Processing failed",
    Level = "Error",
    Exception = ex.ToString(),
    AdditionalData = JsonSerializer.Serialize(requestObj)
};
```

### Metrics (LogPoints / LogBeats)
Time-series data for metrics and monitoring.
```csharp
var point = new LogPoint
{
    Measurement = "cpu_usage",
    Field = 45.2, // Value
    Tag = "server-01" // Dimension
};
await client.LogPointAsync(point);
```

### Relations (Graph)
Link logs or entities together.
```csharp
var relation = new LogRelation
{
    Origin = "order-123",
    Relation = "created_by",
    Subject = "user-456"
};
await client.LogRelationAsync(relation);
```

## Resilience & Performance

The client handles complexity so you don't have to:
-   **Batching**: Logs are aggregated in memory and sent in bulk (gRPC compressed) to reduce network overhead.
-   **Retries**: Transient failures (network blips) are automatically retried with exponential backoff.
-   **Circuit Breaker**: If the server is down, the client stops trying for a while to prevent resource exhaustion and fast-fail.
-   **Compression**: Payloads are compressed using GZip/Lz4 logic automatically if enabled.

## Best Practices for AI Agents

1.  **Prefer `ILogDBClient`**: When generating code for new features, always suggest using the DI pattern with `ILogDBClient`.
2.  **Use `LogAsync`**: The async methods are optimized. Avoid synchronous wrappers if possible.
3.  **Handle Disposal**: Ensure `ILogDBClient` is disposed (or the host is stopped) to flush pending batches before application exit.
4.  **Error Handling**: The client swallows most logging errors to avoid crashing the main application, but check the `OnError` callback in options if debugging is needed.
