using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LogDB.Extensions.Logging
{
    /// <summary>
    /// Fluent query builder for logs
    /// </summary>
    public interface ILogQueryBuilder
    {
        ILogQueryBuilder FromApplication(string application);
        ILogQueryBuilder InEnvironment(string environment);
        ILogQueryBuilder WithLevel(string level);
        ILogQueryBuilder InCollection(string collection);
        ILogQueryBuilder WithCorrelationId(string correlationId);
        ILogQueryBuilder FromSource(string source);
        ILogQueryBuilder ByUser(string email);
        ILogQueryBuilder ByUserId(int userId);
        ILogQueryBuilder WithHttpMethod(string method);
        ILogQueryBuilder WithRequestPath(string path);
        ILogQueryBuilder FromIpAddress(string ipAddress);
        ILogQueryBuilder WithStatusCode(int statusCode);
        ILogQueryBuilder Containing(string searchString);
        ILogQueryBuilder OnlyExceptions();
        ILogQueryBuilder FromDate(DateTime from);
        ILogQueryBuilder ToDate(DateTime to);
        ILogQueryBuilder InDateRange(DateTime from, DateTime to);
        ILogQueryBuilder InLastMinutes(int minutes);
        ILogQueryBuilder InLastHours(int hours);
        ILogQueryBuilder InLastDays(int days);
        ILogQueryBuilder WithLabel(string label);
        ILogQueryBuilder WithLabels(params string[] labels);
        ILogQueryBuilder WithAttribute(string key, string value);
        ILogQueryBuilder Skip(int count);
        ILogQueryBuilder Take(int count);
        ILogQueryBuilder OrderBy(string field, bool ascending = false);
        ILogQueryBuilder OrderByTimestamp(bool ascending = false);
        
        Task<PagedResult<LogDto>> ExecuteAsync(CancellationToken cancellationToken = default);
        Task<LogDto?> FirstOrDefaultAsync(CancellationToken cancellationToken = default);
        Task<int> CountAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Fluent query builder for cache
    /// </summary>
    public interface ILogCacheQueryBuilder
    {
        ILogCacheQueryBuilder WithKeyPattern(string pattern);
        ILogCacheQueryBuilder InCollection(string collection);
        ILogCacheQueryBuilder FromDate(DateTime from);
        ILogCacheQueryBuilder ToDate(DateTime to);
        ILogCacheQueryBuilder InLastMinutes(int minutes);
        ILogCacheQueryBuilder InLastHours(int hours);
        ILogCacheQueryBuilder InLastDays(int days);
        ILogCacheQueryBuilder Skip(int count);
        ILogCacheQueryBuilder Take(int count);
        ILogCacheQueryBuilder OrderBy(string field, bool ascending = false);
        
        Task<PagedResult<LogCacheDto>> ExecuteAsync(CancellationToken cancellationToken = default);
        Task<LogCacheDto?> GetAsync(string key, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Fluent query builder for log points
    /// </summary>
    public interface ILogPointQueryBuilder
    {
        ILogPointQueryBuilder ForMeasurement(string measurement);
        ILogPointQueryBuilder InCollection(string collection);
        ILogPointQueryBuilder FromDate(DateTime from);
        ILogPointQueryBuilder ToDate(DateTime to);
        ILogPointQueryBuilder InLastMinutes(int minutes);
        ILogPointQueryBuilder InLastHours(int hours);
        ILogPointQueryBuilder InLastDays(int days);
        ILogPointQueryBuilder WithTag(string key, string value);
        ILogPointQueryBuilder Skip(int count);
        ILogPointQueryBuilder Take(int count);
        
        Task<PagedResult<LogPointDto>> ExecuteAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Fluent query builder for log relations
    /// </summary>
    public interface ILogRelationQueryBuilder
    {
        ILogRelationQueryBuilder FromOrigin(string origin);
        ILogRelationQueryBuilder ToSubject(string subject);
        ILogRelationQueryBuilder WithRelation(string relation);
        ILogRelationQueryBuilder InCollection(string collection);
        ILogRelationQueryBuilder FromDate(DateTime from);
        ILogRelationQueryBuilder ToDate(DateTime to);
        ILogRelationQueryBuilder InLastDays(int days);
        ILogRelationQueryBuilder Skip(int count);
        ILogRelationQueryBuilder Take(int count);
        
        Task<PagedResult<LogRelationDto>> ExecuteAsync(CancellationToken cancellationToken = default);
        Task<List<RelatedEntityDto>> GetRelatedAsync(string entity, string direction = "both", int depth = 1, CancellationToken cancellationToken = default);
    }
}


