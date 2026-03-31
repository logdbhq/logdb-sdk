using System;
using System.Threading;
using System.Threading.Tasks;
using LogDB.Client.Models;

namespace LogDB.Extensions.Logging
{
    /// <summary>
    /// Interface for LogDB client
    /// </summary>
    public interface ILogDBClient : IAsyncDisposable, IDisposable
    {
        /// <summary>
        /// Send a log entry
        /// </summary>
        Task<LogResponseStatus> LogAsync(Log log, CancellationToken cancellationToken = default);

        /// <summary>
        /// Send a log point (metric)
        /// </summary>
        [Obsolete("LogPoint is coming soon and is currently disabled in the public SDK.")]
        Task<LogResponseStatus> LogPointAsync(LogPoint logPoint, CancellationToken cancellationToken = default);

        /// <summary>
        /// Send a log beat (heartbeat)
        /// </summary>
        Task<LogResponseStatus> LogBeatAsync(LogBeat logBeat, CancellationToken cancellationToken = default);

        /// <summary>
        /// Send a log cache entry
        /// </summary>
        Task<LogResponseStatus> LogCacheAsync(LogCache logCache, CancellationToken cancellationToken = default);

        /// <summary>
        /// Send a log relation
        /// </summary>
        [Obsolete("LogRelation is coming soon and is currently disabled in the public SDK.")]
        Task<LogResponseStatus> LogRelationAsync(LogRelation logRelation, CancellationToken cancellationToken = default);

        /// <summary>
        /// Flush any pending logs
        /// </summary>
        Task FlushAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Send a batch of log entries
        /// </summary>
        Task<LogResponseStatus> SendLogBatchAsync(IReadOnlyList<Log> logs, CancellationToken cancellationToken = default);

        /// <summary>
        /// Send a batch of log points
        /// </summary>
        [Obsolete("LogPoint batch writes are coming soon and are currently disabled in the public SDK.")]
        Task<LogResponseStatus> SendLogPointBatchAsync(IReadOnlyList<LogPoint> logPoints, CancellationToken cancellationToken = default);

        /// <summary>
        /// Send a batch of log beats
        /// </summary>
        Task<LogResponseStatus> SendLogBeatBatchAsync(IReadOnlyList<LogBeat> logBeats, CancellationToken cancellationToken = default);

        /// <summary>
        /// Send a batch of log cache entries
        /// </summary>
        Task<LogResponseStatus> SendLogCacheBatchAsync(IReadOnlyList<LogCache> logCaches, CancellationToken cancellationToken = default);

        /// <summary>
        /// Send a batch of log relations
        /// </summary>
        [Obsolete("LogRelation batch writes are coming soon and are currently disabled in the public SDK.")]
        Task<LogResponseStatus> SendLogRelationBatchAsync(IReadOnlyList<LogRelation> logRelations, CancellationToken cancellationToken = default);
    }
}
