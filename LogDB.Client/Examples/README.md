# LogDB Client Examples

This directory contains examples demonstrating how to use the LogDB .NET client library.

## Examples

### 1. Basic Example (`BasicExample.cs`)
Shows how to use LogDB with Microsoft.Extensions.Logging:
- Basic logging at different levels
- Structured logging with message templates
- Exception logging
- Using scopes for contextual information
- Adding enrichers for automatic metadata

### 2. OpenTelemetry Example (`OpenTelemetryExample.cs`)
Demonstrates OpenTelemetry integration:
- Distributed tracing with activities/spans
- Metrics collection (counters, histograms)
- Correlation between logs, traces, and metrics
- Error handling with trace context
- Using both native and OTLP protocols

### 3. Fluent API Example (`FluentApiExample.cs`)
Shows the legacy fluent API for backward compatibility:
- Creating logs with the builder pattern
- Adding attributes of different types
- Encryption for sensitive data
- Sending metrics (LogPoint)
- Heartbeats (LogBeat)
- Cache entries (LogCache)
- Relationship tracking (LogRelation)

## Running the Examples

1. Update the API key in each example:
   ```csharp
   options.ApiKey = "your-actual-api-key";
   ```

2. Ensure LogDB services are running:
   - gRPC logger service (default: localhost:5001)
   - OTLP collector (default: localhost:4318)
   - REST API (default: localhost:5000)

3. Run an example:
   ```bash
   dotnet run --project LogDB.Examples
   ```

## Configuration

You can configure the LogDB client through:

1. **Code** (as shown in examples)
2. **appsettings.json**:
   ```json
   {
     "Logging": {
       "LogDB": {
         "ApiKey": "your-api-key",
         "ServiceUrl": "https://logdb.yourdomain.com",
         "DefaultCollection": "my-app"
       }
     }
   }
   ```

3. **Environment variables**:
   ```bash
   LOGDB_API_KEY=your-api-key
   LOGDB_SERVICE_URL=https://logdb.yourdomain.com
   LOGDB_DEFAULT_COLLECTION=my-app
   LOGDB_PROTOCOL=Native  # or OpenTelemetry
   ```

## Best Practices

1. **Use Microsoft.Extensions.Logging** for new applications
2. **Add enrichers** to automatically include context (machine name, environment, etc.)
3. **Use structured logging** with message templates instead of string concatenation
4. **Enable batching** for better performance
5. **Configure retry policies** for resilience
6. **Use scopes** to add contextual information to multiple log entries
7. **Choose the right protocol**:
   - Native: Best performance, full feature support
   - OpenTelemetry: Standards-based, works with any OTLP collector
   - REST: Fallback option, useful for debugging

## Troubleshooting

Enable debug logging to see what's happening:

```csharp
options.EnableDebugLogging = true;
options.OnError = (ex, logs) => 
{
    Console.Error.WriteLine($"Failed to send {logs.Count} logs: {ex.Message}");
};
```

