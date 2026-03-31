using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LogDB.Client.Models;

namespace LogDB.Extensions.Logging
{
    /// <summary>
    /// Interface for reading/querying logs from LogDB
    /// </summary>
    public interface ILogDBReader : IAsyncDisposable, IDisposable
    {
        #region Log Queries

        /// <summary>
        /// Query logs with filters
        /// </summary>
        Task<PagedResult<LogDto>> GetLogsAsync(LogQueryParams query, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get a log by ID
        /// </summary>
        Task<LogDto?> GetLogByIdAsync(string id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get a log by GUID
        /// </summary>
        Task<LogDto?> GetLogByGuidAsync(string guid, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get distinct values for a field (e.g., applications, environments, levels)
        /// </summary>
        Task<List<string>> GetDistinctValuesAsync(string field, string? collection = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get log statistics
        /// </summary>
        Task<LogStatsDto> GetLogStatsAsync(LogStatsParams query, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get log time series data
        /// </summary>
        Task<List<TimeSeriesPoint>> GetLogTimeSeriesAsync(LogTimeSeriesParams query, CancellationToken cancellationToken = default);

        #endregion

        #region LogCache Queries

        /// <summary>
        /// Get a cache entry by key
        /// </summary>
        Task<LogCacheDto?> GetLogCacheAsync(string key, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get a cache entry by ID
        /// </summary>
        Task<LogCacheDto?> GetLogCacheByIdAsync(string id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Query cache entries
        /// </summary>
        Task<PagedResult<LogCacheDto>> GetLogCachesAsync(LogCacheQueryParams query, CancellationToken cancellationToken = default);

        #endregion

        #region LogPoint Queries

        /// <summary>
        /// Query log points (metrics)
        /// </summary>
        [Obsolete("LogPoint read APIs are coming soon and are currently disabled in the public SDK.")]
        Task<PagedResult<LogPointDto>> GetLogPointsAsync(LogPointQueryParams query, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get available measurements
        /// </summary>
        Task<List<string>> GetLogPointMeasurementsAsync(string? collection = null, CancellationToken cancellationToken = default);

        #endregion

        #region LogBeat Queries

        /// <summary>
        /// Query log beats (heartbeats)
        /// </summary>
        Task<PagedResult<LogBeatDto>> GetLogBeatsAsync(LogBeatQueryParams query, CancellationToken cancellationToken = default);

        #endregion

        #region LogRelation Queries

        /// <summary>
        /// Query log relations
        /// </summary>
        [Obsolete("LogRelation read APIs are coming soon and are currently disabled in the public SDK.")]
        Task<PagedResult<LogRelationDto>> GetLogRelationsAsync(LogRelationQueryParams query, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get related entities for a given entity
        /// </summary>
        Task<List<RelatedEntityDto>> GetRelatedEntitiesAsync(string entity, string direction = "both", string? relationType = null, int depth = 1, CancellationToken cancellationToken = default);

        #endregion

        #region Windows Events / IIS Events / Metrics Queries

        /// <summary>
        /// Check if the account has Windows Events and/or IIS Events data
        /// </summary>
        Task<EventLogStatusDto> GetEventLogStatusAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Query Windows Events
        /// </summary>
        Task<PagedResult<WindowsEventDto>> GetWindowsEventsAsync(WindowsEventQueryParams query, CancellationToken cancellationToken = default);

        /// <summary>
        /// Query IIS Events
        /// </summary>
        Task<PagedResult<IISEventDto>> GetIISEventsAsync(IISEventQueryParams query, CancellationToken cancellationToken = default);

        /// <summary>
        /// Query Windows Metrics
        /// </summary>
        Task<PagedResult<WindowsMetricsDto>> GetWindowsMetricsAsync(WindowsMetricsQueryParams query, CancellationToken cancellationToken = default);

        #endregion

        #region Fluent Builders

        /// <summary>
        /// Create a fluent query builder for logs
        /// </summary>
        ILogQueryBuilder QueryLogs();

        /// <summary>
        /// Create a fluent query builder for cache
        /// </summary>
        ILogCacheQueryBuilder QueryCache();

        /// <summary>
        /// Create a fluent query builder for log points
        /// </summary>
        [Obsolete("LogPoint fluent query APIs are coming soon and are currently disabled in the public SDK.")]
        ILogPointQueryBuilder QueryLogPoints();

        /// <summary>
        /// Create a fluent query builder for log relations
        /// </summary>
        [Obsolete("LogRelation fluent query APIs are coming soon and are currently disabled in the public SDK.")]
        ILogRelationQueryBuilder QueryRelations();

        #endregion
    }

    #region Query Parameter Classes

    public class LogQueryParams
    {
        public int AccountId { get; set; }
        public string? Application { get; set; }
        public string? Environment { get; set; }
        public string? Level { get; set; }
        public string? Collection { get; set; }
        public string? CorrelationId { get; set; }
        public string? Source { get; set; }
        public string? UserEmail { get; set; }
        public int? UserId { get; set; }
        public string? HttpMethod { get; set; }
        public string? RequestPath { get; set; }
        public string? IpAddress { get; set; }
        public int? StatusCode { get; set; }
        public string? SearchString { get; set; }
        public bool? IsException { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public int Skip { get; set; } = 0;
        public int Take { get; set; } = 50;
        public string SortField { get; set; } = "Timestamp";
        public bool SortAscending { get; set; } = false;
        public List<string>? Labels { get; set; }
        public Dictionary<string, string>? AttributeFilters { get; set; }
    }

    public class LogCacheQueryParams
    {
        public int AccountId { get; set; }
        public string? KeyPattern { get; set; }
        public string? Collection { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public int Skip { get; set; } = 0;
        public int Take { get; set; } = 50;
        public string SortField { get; set; } = "CreatedAt";
        public bool SortAscending { get; set; } = false;
    }

    public class LogPointQueryParams
    {
        public int AccountId { get; set; }
        public string? Measurement { get; set; }
        public string? Collection { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public Dictionary<string, string>? TagFilters { get; set; }
        public int Skip { get; set; } = 0;
        public int Take { get; set; } = 50;
    }

    public class LogBeatQueryParams
    {
        public int AccountId { get; set; }
        public string? Measurement { get; set; }
        public string? Collection { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public Dictionary<string, string>? TagFilters { get; set; }
        public int Skip { get; set; } = 0;
        public int Take { get; set; } = 50;
    }

    public class LogRelationQueryParams
    {
        public int AccountId { get; set; }
        public string? Origin { get; set; }
        public string? Subject { get; set; }
        public string? Relation { get; set; }
        public string? Collection { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public int Skip { get; set; } = 0;
        public int Take { get; set; } = 50;
    }

    public class LogStatsParams
    {
        public string? Collection { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public string? GroupBy { get; set; }
    }

    public class LogTimeSeriesParams
    {
        public string? Collection { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public string Interval { get; set; } = "hour";
        public string? GroupBy { get; set; }
    }

    public class WindowsEventQueryParams
    {
        public int AccountId { get; set; }
        public string? Level { get; set; }
        public string? ProviderName { get; set; }
        public string? Channel { get; set; }
        public string? Computer { get; set; }
        public string? SearchString { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public int Skip { get; set; } = 0;
        public int Take { get; set; } = 50;
    }

    public class IISEventQueryParams
    {
        public int AccountId { get; set; }
        public string? Method { get; set; }
        public int? StatusCode { get; set; }
        public string? UriStem { get; set; }
        public string? ClientIp { get; set; }
        public string? SiteName { get; set; }
        public string? SearchString { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public int Skip { get; set; } = 0;
        public int Take { get; set; } = 50;
    }

    public class WindowsMetricsQueryParams
    {
        public int AccountId { get; set; }
        public string? ServerName { get; set; }
        public string? Environment { get; set; }
        public string? Measurement { get; set; }
        public string? SearchString { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public int Skip { get; set; } = 0;
        public int Take { get; set; } = 50;
    }

    #endregion

    #region Result Classes

    public class PagedResult<T>
    {
        public List<T> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public bool HasMore { get; set; }
    }

    public class LogStatsDto
    {
        public long TotalCount { get; set; }
        public long ErrorCount { get; set; }
        public long WarningCount { get; set; }
        public long InfoCount { get; set; }
        public long DebugCount { get; set; }
        public List<LogStatGroupDto> Groups { get; set; } = new();
    }

    public class LogStatGroupDto
    {
        public string Key { get; set; } = string.Empty;
        public long Count { get; set; }
        public double Percentage { get; set; }
    }

    public class TimeSeriesPoint
    {
        public DateTime Timestamp { get; set; }
        public long Count { get; set; }
        public string? Group { get; set; }
    }

    public class RelatedEntityDto
    {
        public string Entity { get; set; } = string.Empty;
        public string Relation { get; set; } = string.Empty;
        public string Direction { get; set; } = string.Empty;
        public int Depth { get; set; }
        public Dictionary<string, string> Properties { get; set; } = new();
    }

    #endregion

    #region DTO Extensions

    public class LogDto
    {
        public string Id { get; set; } = string.Empty;
        public string? Guid { get; set; }
        public DateTime Timestamp { get; set; }
        public string? Application { get; set; }
        public string? Environment { get; set; }
        public string? Level { get; set; }
        public string? Message { get; set; }
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
        public string? Collection { get; set; }
        public DateTime? DateIn { get; set; }
        public List<string> Labels { get; set; } = new();
        public Dictionary<string, string> AttributeS { get; set; } = new();
        public Dictionary<string, double> AttributeN { get; set; } = new();
        public Dictionary<string, bool> AttributeB { get; set; } = new();
        public Dictionary<string, DateTime> AttributeD { get; set; } = new();

        // AI Analysis
        public string? AiAnalysisId { get; set; }
        public string? AiInsights { get; set; }
        public string? AiRecommendations { get; set; }
        public double? AiConfidenceScore { get; set; }
    }

    public class LogCacheDto
    {
        public string Id { get; set; } = string.Empty;
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string? Collection { get; set; }
        public int? TtlSeconds { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public List<LogCacheMetaDto> Metadata { get; set; } = new();
    }

    public class LogCacheMetaDto
    {
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }

    public class LogPointDto
    {
        public string Id { get; set; } = string.Empty;
        public string? Guid { get; set; }
        public string Measurement { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string? Collection { get; set; }
        public List<LogPointMetaDto> Tags { get; set; } = new();
        public List<LogPointMetaDto> Fields { get; set; } = new();
    }

    public class LogPointMetaDto
    {
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }

    public class LogBeatDto
    {
        public string Id { get; set; } = string.Empty;
        public string? Guid { get; set; }
        public string Measurement { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string? Collection { get; set; }
        public List<LogPointMetaDto> Tags { get; set; } = new();
        public List<LogPointMetaDto> Fields { get; set; } = new();
    }

    public class LogRelationDto
    {
        public string Id { get; set; } = string.Empty;
        public string? Guid { get; set; }
        public string Origin { get; set; } = string.Empty;
        public string Relation { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string? Collection { get; set; }
        public DateTime? DateIn { get; set; }
        public Dictionary<string, string> OriginProperties { get; set; } = new();
        public Dictionary<string, string> SubjectProperties { get; set; } = new();
        public Dictionary<string, string> RelationProperties { get; set; } = new();
    }

    public class EventLogStatusDto
    {
        public bool HasWindowsEvents { get; set; }
        public bool HasIISEvents { get; set; }
    }

    public class WindowsEventDto
    {
        public int Id { get; set; }
        public DateTime TimeCreated { get; set; }
        public string? ProviderName { get; set; }
        public string? Channel { get; set; }
        public string? Task { get; set; }
        public string? Opcode { get; set; }
        public string? Level { get; set; }
        public string? Keywords { get; set; }
        public string? Computer { get; set; }
        public string? UserId { get; set; }
        public string? Message { get; set; }
        public string? XmlData { get; set; }
        public int AccountId { get; set; }
        public DateTime DateIn { get; set; }
        public string? Collection { get; set; }
        public string? IpAddress { get; set; }
    }

    public class IISEventDto
    {
        public int Id { get; set; }
        public DateTime Timestamp { get; set; }
        public string? Method { get; set; }
        public string? StatusCode { get; set; }
        public string? UriStem { get; set; }
        public string? UriQuery { get; set; }
        public string? ClientIp { get; set; }
        public string? ServerIp { get; set; }
        public string? ServerPort { get; set; }
        public string? UserAgent { get; set; }
        public string? TimeTaken { get; set; }
        public string? BytesSent { get; set; }
        public string? BytesReceived { get; set; }
        public string? SiteName { get; set; }
        public string? Username { get; set; }
        public string? Referer { get; set; }
        public string? Host { get; set; }
        public string? ProtocolVersion { get; set; }
        public string? SubStatus { get; set; }
        public string? Win32Status { get; set; }
        public string? SourceFile { get; set; }
        public string? SiteId { get; set; }
        public string? Collection { get; set; }
        public int AccountId { get; set; }
        public DateTime DateIn { get; set; }
    }

    public class WindowsMetricsDto
    {
        public int Id { get; set; }
        public DateTime Timestamp { get; set; }
        public string? Measurement { get; set; }
        public string? ServerName { get; set; }
        public string? Environment { get; set; }
        public double CpuUsagePercent { get; set; }
        public double MemoryUsagePercent { get; set; }
        public double DiskUsagePercent { get; set; }
        public double NetworkSpeedMbps { get; set; }
        public string? Tags { get; set; }
        public string? Fields { get; set; }
        public int AccountId { get; set; }
        public DateTime DateIn { get; set; }
        public string? Collection { get; set; }
        public string? Guid { get; set; }
    }

    #endregion
}


