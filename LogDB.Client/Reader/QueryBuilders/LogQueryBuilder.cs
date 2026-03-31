using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LogDB.Extensions.Logging
{
    public class LogQueryBuilder : ILogQueryBuilder
    {
        private readonly ILogDBReader _reader;
        private readonly LogQueryParams _params = new();

        public LogQueryBuilder(ILogDBReader reader)
        {
            _reader = reader;
        }

        public ILogQueryBuilder FromApplication(string application)
        {
            _params.Application = application;
            return this;
        }

        public ILogQueryBuilder InEnvironment(string environment)
        {
            _params.Environment = environment;
            return this;
        }

        public ILogQueryBuilder WithLevel(string level)
        {
            _params.Level = level;
            return this;
        }

        public ILogQueryBuilder InCollection(string collection)
        {
            _params.Collection = collection;
            return this;
        }

        public ILogQueryBuilder WithCorrelationId(string correlationId)
        {
            _params.CorrelationId = correlationId;
            return this;
        }

        public ILogQueryBuilder FromSource(string source)
        {
            _params.Source = source;
            return this;
        }

        public ILogQueryBuilder ByUser(string email)
        {
            _params.UserEmail = email;
            return this;
        }

        public ILogQueryBuilder ByUserId(int userId)
        {
            _params.UserId = userId;
            return this;
        }

        public ILogQueryBuilder WithHttpMethod(string method)
        {
            _params.HttpMethod = method;
            return this;
        }

        public ILogQueryBuilder WithRequestPath(string path)
        {
            _params.RequestPath = path;
            return this;
        }

        public ILogQueryBuilder FromIpAddress(string ipAddress)
        {
            _params.IpAddress = ipAddress;
            return this;
        }

        public ILogQueryBuilder WithStatusCode(int statusCode)
        {
            _params.StatusCode = statusCode;
            return this;
        }

        public ILogQueryBuilder Containing(string searchString)
        {
            _params.SearchString = searchString;
            return this;
        }

        public ILogQueryBuilder OnlyExceptions()
        {
            _params.IsException = true;
            return this;
        }

        public ILogQueryBuilder FromDate(DateTime from)
        {
            _params.FromDate = from;
            return this;
        }

        public ILogQueryBuilder ToDate(DateTime to)
        {
            _params.ToDate = to;
            return this;
        }

        public ILogQueryBuilder InDateRange(DateTime from, DateTime to)
        {
            _params.FromDate = from;
            _params.ToDate = to;
            return this;
        }

        public ILogQueryBuilder InLastMinutes(int minutes)
        {
            _params.FromDate = DateTime.UtcNow.AddMinutes(-minutes);
            return this;
        }

        public ILogQueryBuilder InLastHours(int hours)
        {
            _params.FromDate = DateTime.UtcNow.AddHours(-hours);
            return this;
        }

        public ILogQueryBuilder InLastDays(int days)
        {
            _params.FromDate = DateTime.UtcNow.AddDays(-days);
            return this;
        }

        public ILogQueryBuilder WithLabel(string label)
        {
            _params.Labels ??= new List<string>();
            _params.Labels.Add(label);
            return this;
        }

        public ILogQueryBuilder WithLabels(params string[] labels)
        {
            _params.Labels ??= new List<string>();
            _params.Labels.AddRange(labels);
            return this;
        }

        public ILogQueryBuilder WithAttribute(string key, string value)
        {
            _params.AttributeFilters ??= new Dictionary<string, string>();
            _params.AttributeFilters[key] = value;
            return this;
        }

        public ILogQueryBuilder Skip(int count)
        {
            _params.Skip = count;
            return this;
        }

        public ILogQueryBuilder Take(int count)
        {
            _params.Take = count;
            return this;
        }

        public ILogQueryBuilder OrderBy(string field, bool ascending = false)
        {
            _params.SortField = field;
            _params.SortAscending = ascending;
            return this;
        }

        public ILogQueryBuilder OrderByTimestamp(bool ascending = false)
        {
            _params.SortField = "Timestamp";
            _params.SortAscending = ascending;
            return this;
        }

        public async Task<PagedResult<LogDto>> ExecuteAsync(CancellationToken cancellationToken = default)
        {
            return await _reader.GetLogsAsync(_params, cancellationToken);
        }

        public async Task<LogDto?> FirstOrDefaultAsync(CancellationToken cancellationToken = default)
        {
            _params.Take = 1;
            var result = await _reader.GetLogsAsync(_params, cancellationToken);
            return result.Items.Count > 0 ? result.Items[0] : null;
        }

        public async Task<int> CountAsync(CancellationToken cancellationToken = default)
        {
            // Keep take positive to avoid divide-by-zero in paging calculations.
            _params.Take = 1;
            var result = await _reader.GetLogsAsync(_params, cancellationToken);
            return result.TotalCount;
        }
    }

    public class LogCacheQueryBuilder : ILogCacheQueryBuilder
    {
        private readonly ILogDBReader _reader;
        private readonly LogCacheQueryParams _params = new();

        public LogCacheQueryBuilder(ILogDBReader reader)
        {
            _reader = reader;
        }

        public ILogCacheQueryBuilder WithKeyPattern(string pattern)
        {
            _params.KeyPattern = pattern;
            return this;
        }

        public ILogCacheQueryBuilder InCollection(string collection)
        {
            _params.Collection = collection;
            return this;
        }

        public ILogCacheQueryBuilder FromDate(DateTime from)
        {
            _params.FromDate = from;
            return this;
        }

        public ILogCacheQueryBuilder ToDate(DateTime to)
        {
            _params.ToDate = to;
            return this;
        }

        public ILogCacheQueryBuilder InLastMinutes(int minutes)
        {
            _params.FromDate = DateTime.UtcNow.AddMinutes(-minutes);
            return this;
        }

        public ILogCacheQueryBuilder InLastHours(int hours)
        {
            _params.FromDate = DateTime.UtcNow.AddHours(-hours);
            return this;
        }

        public ILogCacheQueryBuilder InLastDays(int days)
        {
            _params.FromDate = DateTime.UtcNow.AddDays(-days);
            return this;
        }

        public ILogCacheQueryBuilder Skip(int count)
        {
            _params.Skip = count;
            return this;
        }

        public ILogCacheQueryBuilder Take(int count)
        {
            _params.Take = count;
            return this;
        }

        public ILogCacheQueryBuilder OrderBy(string field, bool ascending = false)
        {
            _params.SortField = field;
            _params.SortAscending = ascending;
            return this;
        }

        public async Task<PagedResult<LogCacheDto>> ExecuteAsync(CancellationToken cancellationToken = default)
        {
            return await _reader.GetLogCachesAsync(_params, cancellationToken);
        }

        public async Task<LogCacheDto?> GetAsync(string key, CancellationToken cancellationToken = default)
        {
            return await _reader.GetLogCacheAsync(key, cancellationToken);
        }
    }

    public class LogPointQueryBuilder : ILogPointQueryBuilder
    {
        private readonly ILogDBReader _reader;
        private readonly LogPointQueryParams _params = new();

        public LogPointQueryBuilder(ILogDBReader reader)
        {
            _reader = reader;
        }

        public ILogPointQueryBuilder ForMeasurement(string measurement)
        {
            _params.Measurement = measurement;
            return this;
        }

        public ILogPointQueryBuilder InCollection(string collection)
        {
            _params.Collection = collection;
            return this;
        }

        public ILogPointQueryBuilder FromDate(DateTime from)
        {
            _params.FromDate = from;
            return this;
        }

        public ILogPointQueryBuilder ToDate(DateTime to)
        {
            _params.ToDate = to;
            return this;
        }

        public ILogPointQueryBuilder InLastMinutes(int minutes)
        {
            _params.FromDate = DateTime.UtcNow.AddMinutes(-minutes);
            return this;
        }

        public ILogPointQueryBuilder InLastHours(int hours)
        {
            _params.FromDate = DateTime.UtcNow.AddHours(-hours);
            return this;
        }

        public ILogPointQueryBuilder InLastDays(int days)
        {
            _params.FromDate = DateTime.UtcNow.AddDays(-days);
            return this;
        }

        public ILogPointQueryBuilder WithTag(string key, string value)
        {
            _params.TagFilters ??= new Dictionary<string, string>();
            _params.TagFilters[key] = value;
            return this;
        }

        public ILogPointQueryBuilder Skip(int count)
        {
            _params.Skip = count;
            return this;
        }

        public ILogPointQueryBuilder Take(int count)
        {
            _params.Take = count;
            return this;
        }

        public async Task<PagedResult<LogPointDto>> ExecuteAsync(CancellationToken cancellationToken = default)
        {
            return await _reader.GetLogPointsAsync(_params, cancellationToken);
        }
    }

    public class LogRelationQueryBuilder : ILogRelationQueryBuilder
    {
        private readonly ILogDBReader _reader;
        private readonly LogRelationQueryParams _params = new();

        public LogRelationQueryBuilder(ILogDBReader reader)
        {
            _reader = reader;
        }

        public ILogRelationQueryBuilder FromOrigin(string origin)
        {
            _params.Origin = origin;
            return this;
        }

        public ILogRelationQueryBuilder ToSubject(string subject)
        {
            _params.Subject = subject;
            return this;
        }

        public ILogRelationQueryBuilder WithRelation(string relation)
        {
            _params.Relation = relation;
            return this;
        }

        public ILogRelationQueryBuilder InCollection(string collection)
        {
            _params.Collection = collection;
            return this;
        }

        public ILogRelationQueryBuilder FromDate(DateTime from)
        {
            _params.FromDate = from;
            return this;
        }

        public ILogRelationQueryBuilder ToDate(DateTime to)
        {
            _params.ToDate = to;
            return this;
        }

        public ILogRelationQueryBuilder InLastDays(int days)
        {
            _params.FromDate = DateTime.UtcNow.AddDays(-days);
            return this;
        }

        public ILogRelationQueryBuilder Skip(int count)
        {
            _params.Skip = count;
            return this;
        }

        public ILogRelationQueryBuilder Take(int count)
        {
            _params.Take = count;
            return this;
        }

        public async Task<PagedResult<LogRelationDto>> ExecuteAsync(CancellationToken cancellationToken = default)
        {
            return await _reader.GetLogRelationsAsync(_params, cancellationToken);
        }

        public async Task<List<RelatedEntityDto>> GetRelatedAsync(string entity, string direction = "both", int depth = 1, CancellationToken cancellationToken = default)
        {
            return await _reader.GetRelatedEntitiesAsync(entity, direction, _params.Relation, depth, cancellationToken);
        }
    }
}


