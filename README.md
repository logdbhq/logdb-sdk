# LogDB.SDK

.NET SDK repository for LogDB:
- `LogDB.Client`
- `LogDB.Serilog`
- `LogDB.NLog`

This README is aligned with `logdocs` (`/sdk/client`, `/sdk/serilog`, `/sdk/nlog`) as of **2026-03-31**.

## Package Snapshot

| Package | Current version | Frameworks |
|---|---:|---|
| `LogDB.Client` | `5.1.1` | `net472`, `net8.0`, `net9.0`, `net10.0` |
| `LogDB.Serilog` | `5.1.1` | `net472`, `net8.0`, `net9.0`, `net10.0` |
| `LogDB.NLog` | `5.1.1` | `net472`, `net8.0`, `net9.0`, `net10.0` |

NuGet source:

```txt
https://api.nuget.org/v3/index.json
```

## Install

```bash
dotnet add package LogDB.Client
```

`nuget.config`:

```xml
<configuration>
  <packageSources>
    <clear />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
```

## Data Type Status

- Stable now: `Log`, `LogCache`, `LogBeat`
- Marked `[Soon]` in public build: `LogPoint`, `LogRelation`
- `[Soon]` methods currently throw `NotSupportedException` (write and read paths)

Use cases:
- `Log`: rich event records (errors, audits, request traces)
- `LogCache`: latest mutable state / key-value with TTL
- `LogBeat`: heartbeat and lightweight health cadence
- `LogPoint` `[Soon]`: time-series metrics
- `LogRelation` `[Soon]`: graph edges (`origin - relation - subject`)

## Quick Start (Write)

```csharp
using LogDB.Extensions.Logging;
using LogDB.Client.Models;

builder.Logging.AddLogDB(options =>
{
    options.ApiKey = "your-api-key";
    options.DefaultApplication = "Orders.Api";
    options.DefaultEnvironment = "production";
    options.DefaultCollection = "app-logs";
    options.EnableBatching = true;
    options.BatchSize = 100;
    options.FlushInterval = TimeSpan.FromSeconds(5);
});

var app = builder.Build();
var client = app.Services.GetRequiredService<ILogDBClient>();

await client.LogAsync(new Log
{
    Level = LogLevel.Info,
    Message = "Service started",
    Collection = "startup"
});

await client.LogBeatAsync(new LogBeat
{
    Measurement = "service_heartbeat",
    Collection = "heartbeats"
});

await client.FlushAsync();
```

## Encryption (Write only)

Set encryption key before using encrypted fields:

```powershell
$env:LOGDB_SECRET_KEY = "replace-with-random-secret-at-least-32-chars"
```

Options:
1. Encrypt selected parameters with `isEncrypted: true` in builder APIs.
2. Encrypt whole payload with `.Encrypt()` in builder APIs.
3. If writing plain model objects via `ILogDBClient`, manually encrypt values using `EncryptionService.Encrypt(...)`.

## Quick Start (Read)

Account scope is resolved from API key/token on the server side.  
Do **not** hardcode `AccountId` in SDK client setup.

```csharp
using LogDB.Extensions.Logging;

// Recommended: rely on SDK auto-discovery
using var reader = LogDBReaderExtensions.CreateReader("your-api-key");

// Optional: fetch discovered reader URL explicitly (diagnostics/pinning)
// var readerServiceUrl = await LogDBReaderExtensions.DiscoverReaderServiceUrlAsync();
// using var reader = LogDBReaderExtensions.CreateReader("your-api-key", readerServiceUrl);

var recentErrors = await reader.QueryLogs()
    .FromApplication("Orders.Api")
    .WithLevel("Error")
    .InLastHours(4)
    .Take(100)
    .ExecuteAsync();
```

Compatibility note:
- If legacy grpc-server returns `Customer 0 not found`, update grpc-server to a build that resolves account scope from API key in `GetUserAccounts`.
- Set `LOGDB_GRPC_SERVER_URL` to force a specific reader endpoint when needed.

## API Map

Write:
- Single: `LogAsync`, `LogPointAsync [Soon]`, `LogBeatAsync`, `LogCacheAsync`, `LogRelationAsync [Soon]`
- Batch: `SendLogBatchAsync`, `SendLogPointBatchAsync [Soon]`, `SendLogBeatBatchAsync`, `SendLogCacheBatchAsync`, `SendLogRelationBatchAsync [Soon]`
- Utility: `FlushAsync`

Read:
- Direct: `GetLogsAsync`, `GetLogCachesAsync`, `GetLogPointsAsync [Soon]`, `GetLogBeatsAsync`, `GetLogRelationsAsync [Soon]`
- Specialized: `GetWindowsEventsAsync`, `GetIISEventsAsync`, `GetWindowsMetricsAsync`, `GetEventLogStatusAsync`
- Fluent: `QueryLogs()`, `QueryCache()`, `QueryLogPoints() [Soon]`, `QueryRelations() [Soon]`

## Integrations

Serilog:
- Entry point: `WriteTo.LogDB(...)`

NLog:
- Entry point: `LogDBTarget` / `xsi:type="LogDB"`

## Package Publishing (NuGet.org)

Workflows:
- `.github/workflows/publish-client-on-tag.yml`
- `.github/workflows/publish-serilog-on-tag.yml`
- `.github/workflows/publish-nlog-on-tag.yml`

Tag triggers (push):
- `client-v*` publishes `LogDB.Client` (e.g. `client-v5.1.1`)
- `serilog-v*` publishes `LogDB.Serilog` (e.g. `serilog-v5.1.1`)
- `nlog-v*` publishes `LogDB.NLog` (e.g. `nlog-v5.1.1`)

Each workflow also supports manual `workflow_dispatch` with `version` input.

Required repository secrets:
- `NUGET_API_KEY`

NuGet.org setup:
- Create an account or sign in at `nuget.org`
- Create an API key with `Push` scope
- Prefer a package scope such as `LogDB.*` instead of `*`
- Store that value in the GitHub repository secret `NUGET_API_KEY`



