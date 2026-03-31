using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LogDB.Client.Models;

namespace LogDB.Extensions.Logging
{
    /// <summary>
    /// Interface for protocol-specific client implementations
    /// </summary>
    internal interface IProtocolClient
    {
        // Single item methods
        Task<LogResponseStatus> SendLogAsync(Log log, CancellationToken cancellationToken = default);
        Task<LogResponseStatus> SendLogPointAsync(LogPoint logPoint, CancellationToken cancellationToken = default);
        Task<LogResponseStatus> SendLogBeatAsync(LogBeat logBeat, CancellationToken cancellationToken = default);
        Task<LogResponseStatus> SendLogCacheAsync(LogCache logCache, CancellationToken cancellationToken = default);
        Task<LogResponseStatus> SendLogRelationAsync(LogRelation logRelation, CancellationToken cancellationToken = default);

        // Batch methods
        Task<LogResponseStatus> SendLogBatchAsync(IReadOnlyList<Log> logs, CancellationToken cancellationToken = default);
        Task<LogResponseStatus> SendLogPointBatchAsync(IReadOnlyList<LogPoint> logPoints, CancellationToken cancellationToken = default);
        Task<LogResponseStatus> SendLogBeatBatchAsync(IReadOnlyList<LogBeat> logBeats, CancellationToken cancellationToken = default);
        Task<LogResponseStatus> SendLogCacheBatchAsync(IReadOnlyList<LogCache> logCaches, CancellationToken cancellationToken = default);
        Task<LogResponseStatus> SendLogRelationBatchAsync(IReadOnlyList<LogRelation> logRelations, CancellationToken cancellationToken = default);
    }
}

