using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LogDB.Client.Models;
using LogDB.Extensions.Logging;

namespace LogDB.Client.Tests.TestDoubles;

internal sealed class FakeLogDBClient : ILogDBClient
{
    private readonly object _sync = new();

    public List<Log> Logs { get; } = new();
    public List<LogPoint> LogPoints { get; } = new();
    public List<LogBeat> LogBeats { get; } = new();
    public List<LogCache> LogCaches { get; } = new();
    public List<LogRelation> LogRelations { get; } = new();

    public int FlushCount { get; private set; }

    public Task<LogResponseStatus> LogAsync(Log log, CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            Logs.Add(log);
        }

        return Task.FromResult(LogResponseStatus.Success);
    }

    public Task<LogResponseStatus> LogPointAsync(LogPoint logPoint, CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            LogPoints.Add(logPoint);
        }

        return Task.FromResult(LogResponseStatus.Success);
    }

    public Task<LogResponseStatus> LogBeatAsync(LogBeat logBeat, CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            LogBeats.Add(logBeat);
        }

        return Task.FromResult(LogResponseStatus.Success);
    }

    public Task<LogResponseStatus> LogCacheAsync(LogCache logCache, CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            LogCaches.Add(logCache);
        }

        return Task.FromResult(LogResponseStatus.Success);
    }

    public Task<LogResponseStatus> LogRelationAsync(LogRelation logRelation, CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            LogRelations.Add(logRelation);
        }

        return Task.FromResult(LogResponseStatus.Success);
    }

    public Task FlushAsync(CancellationToken cancellationToken = default)
    {
        FlushCount++;
        return Task.CompletedTask;
    }

    public Task<LogResponseStatus> SendLogBatchAsync(IReadOnlyList<Log> logs, CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            Logs.AddRange(logs);
        }

        return Task.FromResult(LogResponseStatus.Success);
    }

    public Task<LogResponseStatus> SendLogPointBatchAsync(IReadOnlyList<LogPoint> logPoints, CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            LogPoints.AddRange(logPoints);
        }

        return Task.FromResult(LogResponseStatus.Success);
    }

    public Task<LogResponseStatus> SendLogBeatBatchAsync(IReadOnlyList<LogBeat> logBeats, CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            LogBeats.AddRange(logBeats);
        }

        return Task.FromResult(LogResponseStatus.Success);
    }

    public Task<LogResponseStatus> SendLogCacheBatchAsync(IReadOnlyList<LogCache> logCaches, CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            LogCaches.AddRange(logCaches);
        }

        return Task.FromResult(LogResponseStatus.Success);
    }

    public Task<LogResponseStatus> SendLogRelationBatchAsync(IReadOnlyList<LogRelation> logRelations, CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            LogRelations.AddRange(logRelations);
        }

        return Task.FromResult(LogResponseStatus.Success);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    public void Dispose()
    {
    }
}
