using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LogDB.Extensions.Logging;

namespace LogDB.Client.Tests.TestDoubles;

internal sealed class FakeLogDBReader : ILogDBReader
{
    public LogQueryParams? LastLogQuery { get; private set; }
    public LogCacheQueryParams? LastCacheQuery { get; private set; }
    public LogPointQueryParams? LastPointQuery { get; private set; }
    public LogRelationQueryParams? LastRelationQuery { get; private set; }
    public string? LastCacheKey { get; private set; }
    public (string Entity, string Direction, string? RelationType, int Depth)? LastRelatedEntitiesRequest { get; private set; }

    public PagedResult<LogDto> NextLogsResult { get; set; } = new();
    public PagedResult<LogCacheDto> NextCachesResult { get; set; } = new();
    public PagedResult<LogPointDto> NextPointsResult { get; set; } = new();
    public PagedResult<LogBeatDto> NextBeatsResult { get; set; } = new();
    public PagedResult<LogRelationDto> NextRelationsResult { get; set; } = new();
    public LogCacheDto? NextCacheResult { get; set; }
    public List<RelatedEntityDto> NextRelatedEntities { get; set; } = new();

    public Task<PagedResult<LogDto>> GetLogsAsync(LogQueryParams query, CancellationToken cancellationToken = default)
    {
        LastLogQuery = query;
        return Task.FromResult(NextLogsResult);
    }

    public Task<LogDto?> GetLogByIdAsync(string id, CancellationToken cancellationToken = default) => Task.FromResult<LogDto?>(null);

    public Task<LogDto?> GetLogByGuidAsync(string guid, CancellationToken cancellationToken = default) => Task.FromResult<LogDto?>(null);

    public Task<List<string>> GetDistinctValuesAsync(string field, string? collection = null, CancellationToken cancellationToken = default) =>
        Task.FromResult(new List<string>());

    public Task<LogStatsDto> GetLogStatsAsync(LogStatsParams query, CancellationToken cancellationToken = default) =>
        Task.FromResult(new LogStatsDto());

    public Task<List<TimeSeriesPoint>> GetLogTimeSeriesAsync(LogTimeSeriesParams query, CancellationToken cancellationToken = default) =>
        Task.FromResult(new List<TimeSeriesPoint>());

    public Task<LogCacheDto?> GetLogCacheAsync(string key, CancellationToken cancellationToken = default)
    {
        LastCacheKey = key;
        return Task.FromResult(NextCacheResult);
    }

    public Task<LogCacheDto?> GetLogCacheByIdAsync(string id, CancellationToken cancellationToken = default) =>
        Task.FromResult<LogCacheDto?>(null);

    public Task<PagedResult<LogCacheDto>> GetLogCachesAsync(LogCacheQueryParams query, CancellationToken cancellationToken = default)
    {
        LastCacheQuery = query;
        return Task.FromResult(NextCachesResult);
    }

    public Task<PagedResult<LogPointDto>> GetLogPointsAsync(LogPointQueryParams query, CancellationToken cancellationToken = default)
    {
        LastPointQuery = query;
        return Task.FromResult(NextPointsResult);
    }

    public Task<List<string>> GetLogPointMeasurementsAsync(string? collection = null, CancellationToken cancellationToken = default) =>
        Task.FromResult(new List<string>());

    public Task<PagedResult<LogBeatDto>> GetLogBeatsAsync(LogBeatQueryParams query, CancellationToken cancellationToken = default) =>
        Task.FromResult(NextBeatsResult);

    public Task<PagedResult<LogRelationDto>> GetLogRelationsAsync(LogRelationQueryParams query, CancellationToken cancellationToken = default)
    {
        LastRelationQuery = query;
        return Task.FromResult(NextRelationsResult);
    }

    public Task<List<RelatedEntityDto>> GetRelatedEntitiesAsync(string entity, string direction = "both", string? relationType = null, int depth = 1, CancellationToken cancellationToken = default)
    {
        LastRelatedEntitiesRequest = (entity, direction, relationType, depth);
        return Task.FromResult(NextRelatedEntities);
    }

    public Task<EventLogStatusDto> GetEventLogStatusAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new EventLogStatusDto());

    public Task<PagedResult<WindowsEventDto>> GetWindowsEventsAsync(WindowsEventQueryParams query, CancellationToken cancellationToken = default) =>
        Task.FromResult(new PagedResult<WindowsEventDto>());

    public Task<PagedResult<IISEventDto>> GetIISEventsAsync(IISEventQueryParams query, CancellationToken cancellationToken = default) =>
        Task.FromResult(new PagedResult<IISEventDto>());

    public Task<PagedResult<WindowsMetricsDto>> GetWindowsMetricsAsync(WindowsMetricsQueryParams query, CancellationToken cancellationToken = default) =>
        Task.FromResult(new PagedResult<WindowsMetricsDto>());

    public ILogQueryBuilder QueryLogs() => new LogQueryBuilder(this);

    public ILogCacheQueryBuilder QueryCache() => new LogCacheQueryBuilder(this);

    public ILogPointQueryBuilder QueryLogPoints() => new LogPointQueryBuilder(this);

    public ILogRelationQueryBuilder QueryRelations() => new LogRelationQueryBuilder(this);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    public void Dispose()
    {
    }
}
