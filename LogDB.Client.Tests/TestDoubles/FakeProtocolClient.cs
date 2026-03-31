using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LogDB.Client.Models;
using LogDB.Extensions.Logging;

namespace LogDB.Client.Tests.TestDoubles;

internal class FakeProtocolClient : IProtocolClient
{
    public List<Log> SentLogs { get; } = new();
    public List<LogBeat> SentLogBeats { get; } = new();
    public List<LogCache> SentLogCaches { get; } = new();

    public int SendLogCallCount { get; private set; }
    public int SendLogBatchCallCount { get; private set; }

    public Exception? ExceptionToThrow { get; set; }
    public Queue<Exception> SendLogExceptions { get; } = new();
    public Queue<Exception> SendLogBatchExceptions { get; } = new();

    public Task<LogResponseStatus> SendLogAsync(Log log, CancellationToken cancellationToken = default)
    {
        SendLogCallCount++;
        if (SendLogExceptions.Count > 0) throw SendLogExceptions.Dequeue();
        if (ExceptionToThrow != null) throw ExceptionToThrow;
        SentLogs.Add(log);
        return Task.FromResult(LogResponseStatus.Success);
    }

    public Task<LogResponseStatus> SendLogPointAsync(LogPoint logPoint, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(LogResponseStatus.Success);
    }

    public Task<LogResponseStatus> SendLogBeatAsync(LogBeat logBeat, CancellationToken cancellationToken = default)
    {
        if (ExceptionToThrow != null) throw ExceptionToThrow;
        SentLogBeats.Add(logBeat);
        return Task.FromResult(LogResponseStatus.Success);
    }

    public Task<LogResponseStatus> SendLogCacheAsync(LogCache logCache, CancellationToken cancellationToken = default)
    {
        if (ExceptionToThrow != null) throw ExceptionToThrow;
        SentLogCaches.Add(logCache);
        return Task.FromResult(LogResponseStatus.Success);
    }

    public Task<LogResponseStatus> SendLogRelationAsync(LogRelation logRelation, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(LogResponseStatus.Success);
    }

    public Task<LogResponseStatus> SendLogBatchAsync(IReadOnlyList<Log> logs, CancellationToken cancellationToken = default)
    {
        SendLogBatchCallCount++;
        if (SendLogBatchExceptions.Count > 0) throw SendLogBatchExceptions.Dequeue();
        if (ExceptionToThrow != null) throw ExceptionToThrow;
        SentLogs.AddRange(logs);
        return Task.FromResult(LogResponseStatus.Success);
    }

    public Task<LogResponseStatus> SendLogPointBatchAsync(IReadOnlyList<LogPoint> logPoints, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(LogResponseStatus.Success);
    }

    public Task<LogResponseStatus> SendLogBeatBatchAsync(IReadOnlyList<LogBeat> logBeats, CancellationToken cancellationToken = default)
    {
        if (ExceptionToThrow != null) throw ExceptionToThrow;
        SentLogBeats.AddRange(logBeats);
        return Task.FromResult(LogResponseStatus.Success);
    }

    public Task<LogResponseStatus> SendLogCacheBatchAsync(IReadOnlyList<LogCache> logCaches, CancellationToken cancellationToken = default)
    {
        if (ExceptionToThrow != null) throw ExceptionToThrow;
        SentLogCaches.AddRange(logCaches);
        return Task.FromResult(LogResponseStatus.Success);
    }

    public Task<LogResponseStatus> SendLogRelationBatchAsync(IReadOnlyList<LogRelation> logRelations, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(LogResponseStatus.Success);
    }
}
