using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using LogDB.Grpc;
using LogDB.Client.Services;
using Google.Protobuf.WellKnownTypes;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LogDB.Extensions.Logging
{
    /// <summary>
    /// LogDB Reader client for querying logs via grpc-server
    /// </summary>
    public class LogDBReader : ILogDBReader
    {
        private readonly LogDBLoggerOptions _options;
        private readonly ILogger<LogDBReader>? _logger;
        private readonly LogGrpcServerService.LogGrpcServerServiceClient _client;
        private readonly GrpcChannel _channel;
        private readonly ICompressionService _compressionService;
        private bool _disposed;

        public LogDBReader(IOptions<LogDBLoggerOptions> options, ILogger<LogDBReader>? logger = null)
        {
            _options = options.Value;
            _logger = logger;
            _compressionService = new CompressionService();

            // Get grpc-server service URL
            var serviceUrl = GetServerServiceUrl();

            // Use native gRPC over HTTP/2
#if NETFRAMEWORK
            // Try WinHttpHandler (HTTP/2) first, fall back to GrpcWebHandler (HTTP/1.1) on older Windows
            System.Net.Http.HttpMessageHandler httpHandler;
            try
            {
                var winHttpHandler = new System.Net.Http.WinHttpHandler();
                if (_options.DangerouslyAcceptAnyServerCertificate)
                    winHttpHandler.ServerCertificateValidationCallback = (_, _, _, _) => true;
                using var testChannel = GrpcChannel.ForAddress(serviceUrl, new GrpcChannelOptions { HttpHandler = winHttpHandler });
                httpHandler = winHttpHandler;
            }
            catch
            {
                var fallbackHandler = new System.Net.Http.HttpClientHandler();
                if (_options.DangerouslyAcceptAnyServerCertificate)
                    fallbackHandler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;
                httpHandler = new global::Grpc.Net.Client.Web.GrpcWebHandler(global::Grpc.Net.Client.Web.GrpcWebMode.GrpcWeb, fallbackHandler);
            }
#else
            var httpHandler = new SocketsHttpHandler
            {
                EnableMultipleHttp2Connections = true,
                AutomaticDecompression = DecompressionMethods.All
            };

            // Only disable SSL validation if explicitly configured (for local development only)
            if (_options.DangerouslyAcceptAnyServerCertificate)
            {
                httpHandler.SslOptions.RemoteCertificateValidationCallback = (_, _, _, _) => true;
            }
#endif

            // Create gRPC channel
            _channel = GrpcChannel.ForAddress(serviceUrl, new GrpcChannelOptions
            {
                HttpHandler = httpHandler,
                MaxReceiveMessageSize = 100 * 1024 * 1024, // 100MB
                MaxSendMessageSize = 100 * 1024 * 1024
            });

            _client = new LogGrpcServerService.LogGrpcServerServiceClient(_channel);
        }

        private string GetServerServiceUrl()
        {
            // First check for explicit ReaderServiceUrl (backward compat)
            if (!string.IsNullOrEmpty(_options.ReaderServiceUrl))
            {
                return _options.ReaderServiceUrl;
            }

            // Use discovery service for grpc-server
            try
            {
                var discoveryService = new DiscoveryService();
                var url = discoveryService.DiscoverServiceUrl("grpc-server", _options.ApiKey);
                if (string.IsNullOrEmpty(url))
                {
                    throw new InvalidOperationException(
                        "Unable to discover LogDB Server endpoint. " +
                        "Please set ReaderServiceUrl in options or configure LOGDB_GRPC_SERVER_URL environment variable.");
                }
                _logger?.LogDebug("Discovered LogDB Server service at {Url}", url);
                return url;
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "Failed to discover LogDB Server endpoint. " +
                    "Please set ReaderServiceUrl in options or configure LOGDB_GRPC_SERVER_URL environment variable.", ex);
            }
        }

        #region Log Queries

        public async Task<PagedResult<LogDto>> GetLogsAsync(LogQueryParams query, CancellationToken cancellationToken = default)
        {
            var request = new FilteredLogRequest
            {
                Token = _options.ApiKey,
                AccountId = query.AccountId,
                Application = query.Application ?? "",
                Environment = query.Environment ?? "",
                Level = query.Level ?? "",
                CorrelationId = query.CorrelationId ?? "",
                Source = query.Source ?? "",
                UserEmail = query.UserEmail ?? "",
                UserId = query.UserId ?? 0,
                HttpMethod = query.HttpMethod ?? "",
                RequestPath = query.RequestPath ?? "",
                IpAddress = query.IpAddress ?? "",
                StatusCode = query.StatusCode ?? 0,
                SearchString = query.SearchString ?? "",
                Skip = query.Skip,
                Take = query.Take,
                SortInfo = new SortInfo
                {
                    Field = query.SortField,
                    Ascending = query.SortAscending
                }
            };

            // Collection is now a repeated field
            if (!string.IsNullOrEmpty(query.Collection))
                request.Collections.Add(query.Collection);

            // Dates are now Timestamp type
            if (query.FromDate.HasValue)
                request.FromDate = Timestamp.FromDateTime(DateTime.SpecifyKind(query.FromDate.Value, DateTimeKind.Utc));
            if (query.ToDate.HasValue)
                request.ToDate = Timestamp.FromDateTime(DateTime.SpecifyKind(query.ToDate.Value, DateTimeKind.Utc));

            var response = await _client.GetLogsFilteredAsync(request, cancellationToken: cancellationToken);
            var paging = BuildPaging(query.Skip, query.Take, response.LogEntries.Count);

            return new PagedResult<LogDto>
            {
                Items = response.LogEntries.Select(MapToLogDto).ToList(),
                TotalCount = response.TotalCount,
                Page = paging.Page,
                PageSize = paging.PageSize,
                HasMore = paging.HasMore
            };
        }

        [Obsolete("GetLogByIdAsync is not supported in grpc-server. Use GetLogsAsync with appropriate filters.")]
        public Task<LogDto?> GetLogByIdAsync(string id, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("GetLogByIdAsync is not available in grpc-server. Use GetLogsAsync with appropriate filters.");
        }

        [Obsolete("GetLogByGuidAsync is not supported in grpc-server. Use GetLogsAsync with appropriate filters.")]
        public Task<LogDto?> GetLogByGuidAsync(string guid, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("GetLogByGuidAsync is not available in grpc-server. Use GetLogsAsync with appropriate filters.");
        }

        public async Task<List<string>> GetDistinctValuesAsync(string field, string? collection = null, CancellationToken cancellationToken = default)
        {
            // grpc-server only supports GetDistinctCollections, not generic distinct values
            if (field.Equals("collection", StringComparison.OrdinalIgnoreCase) ||
                field.Equals("collections", StringComparison.OrdinalIgnoreCase))
            {
                var request = new AccessTokenRequest
                {
                    Token = _options.ApiKey
                };

                var response = await _client.GetDistinctCollectionsAsync(request, cancellationToken: cancellationToken);
                return response.Collections.ToList();
            }

            throw new NotSupportedException($"GetDistinctValuesAsync for field '{field}' is not available in grpc-server. Only 'collection' field is supported.");
        }

        [Obsolete("GetLogStatsAsync is not supported in grpc-server.")]
        public Task<LogStatsDto> GetLogStatsAsync(LogStatsParams query, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("GetLogStatsAsync is not available in grpc-server.");
        }

        [Obsolete("GetLogTimeSeriesAsync is not supported in grpc-server.")]
        public Task<List<TimeSeriesPoint>> GetLogTimeSeriesAsync(LogTimeSeriesParams query, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("GetLogTimeSeriesAsync is not available in grpc-server.");
        }

        #endregion

        #region LogCache Queries

        [Obsolete("GetLogCacheAsync by key is not supported in grpc-server. Use GetLogCachesAsync with key filter.")]
        public Task<LogCacheDto?> GetLogCacheAsync(string key, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("GetLogCacheAsync by key is not available in grpc-server. Use GetLogCachesAsync with key filter.");
        }

        [Obsolete("GetLogCacheByIdAsync is not supported in grpc-server. Use GetLogCachesAsync with appropriate filters.")]
        public Task<LogCacheDto?> GetLogCacheByIdAsync(string id, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("GetLogCacheByIdAsync is not available in grpc-server. Use GetLogCachesAsync with key filter.");
        }

        public async Task<PagedResult<LogCacheDto>> GetLogCachesAsync(LogCacheQueryParams query, CancellationToken cancellationToken = default)
        {
            var request = new FilteredLogCacheRequest
            {
                Token = _options.ApiKey,
                AccountId = query.AccountId,
                Key = query.KeyPattern ?? "",
                Skip = query.Skip,
                Take = query.Take
            };

            // Dates are now Timestamp type
            if (query.FromDate.HasValue)
                request.FromDate = Timestamp.FromDateTime(DateTime.SpecifyKind(query.FromDate.Value, DateTimeKind.Utc));
            if (query.ToDate.HasValue)
                request.ToDate = Timestamp.FromDateTime(DateTime.SpecifyKind(query.ToDate.Value, DateTimeKind.Utc));

            var response = await _client.GetLogCachesFilteredAsync(request, cancellationToken: cancellationToken);
            var paging = BuildPaging(query.Skip, query.Take, response.LogCacheEntries.Count);

            return new PagedResult<LogCacheDto>
            {
                Items = response.LogCacheEntries.Select(MapToLogCacheDto).ToList(),
                TotalCount = response.TotalCount,
                Page = paging.Page,
                PageSize = paging.PageSize,
                HasMore = paging.HasMore
            };
        }

        #endregion

        #region LogPoint Queries

        [Obsolete("LogPoint read APIs are coming soon and are currently disabled in the public SDK.")]
        public Task<PagedResult<LogPointDto>> GetLogPointsAsync(LogPointQueryParams query, CancellationToken cancellationToken = default)
        {
            return Task.FromException<PagedResult<LogPointDto>>(
                new NotSupportedException("LogPoint read APIs are marked [Soon] and are not available in this public SDK build yet."));
        }

        [Obsolete("GetLogPointMeasurementsAsync is not supported in grpc-server.")]
        public Task<List<string>> GetLogPointMeasurementsAsync(string? collection = null, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("GetLogPointMeasurementsAsync is not available in grpc-server.");
        }

        #endregion

        #region LogBeat Queries

        public async Task<PagedResult<LogBeatDto>> GetLogBeatsAsync(LogBeatQueryParams query, CancellationToken cancellationToken = default)
        {
            var request = new FilteredLogBeatRequest
            {
                Token = _options.ApiKey,
                AccountId = query.AccountId,
                Measurement = query.Measurement ?? "",
                Skip = query.Skip,
                Take = query.Take
            };

            // Collection is now a repeated field
            if (!string.IsNullOrEmpty(query.Collection))
                request.Collections.Add(query.Collection);

            // Dates are now Timestamp type
            if (query.FromDate.HasValue)
                request.FromDate = Timestamp.FromDateTime(DateTime.SpecifyKind(query.FromDate.Value, DateTimeKind.Utc));
            if (query.ToDate.HasValue)
                request.ToDate = Timestamp.FromDateTime(DateTime.SpecifyKind(query.ToDate.Value, DateTimeKind.Utc));

            var response = await _client.GetLogBeatsFilteredAsync(request, cancellationToken: cancellationToken);
            var paging = BuildPaging(query.Skip, query.Take, response.LogBeatEntries.Count);

            return new PagedResult<LogBeatDto>
            {
                // LogBeatEntry uses LogPointEntry in the proto
                Items = response.LogBeatEntries.Select(MapToLogBeatDto).ToList(),
                TotalCount = response.TotalCount,
                Page = paging.Page,
                PageSize = paging.PageSize,
                HasMore = paging.HasMore
            };
        }

        #endregion

        #region LogRelation Queries

        [Obsolete("LogRelation read APIs are coming soon and are currently disabled in the public SDK.")]
        public Task<PagedResult<LogRelationDto>> GetLogRelationsAsync(LogRelationQueryParams query, CancellationToken cancellationToken = default)
        {
            return Task.FromException<PagedResult<LogRelationDto>>(
                new NotSupportedException("LogRelation read APIs are marked [Soon] and are not available in this public SDK build yet."));
        }

        [Obsolete("GetRelatedEntitiesAsync is not supported in grpc-server.")]
        public Task<List<RelatedEntityDto>> GetRelatedEntitiesAsync(string entity, string direction = "both", string? relationType = null, int depth = 1, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("GetRelatedEntitiesAsync is not available in grpc-server.");
        }

        #endregion

        #region Windows Events / IIS Events / Metrics Queries

        public async Task<EventLogStatusDto> GetEventLogStatusAsync(CancellationToken cancellationToken = default)
        {
            var request = new EventLogStatusRequest
            {
                Token = _options.ApiKey
            };

            var response = await _client.GetEventLogStatusAsync(request, cancellationToken: cancellationToken);

            return new EventLogStatusDto
            {
                HasWindowsEvents = response.HasWindowsEvents,
                HasIISEvents = response.HasIISEvents
            };
        }

        public async Task<PagedResult<WindowsEventDto>> GetWindowsEventsAsync(WindowsEventQueryParams query, CancellationToken cancellationToken = default)
        {
            var request = new FilteredLogRequest
            {
                Token = _options.ApiKey,
                AccountId = query.AccountId,
                Level = query.Level ?? "",
                SearchString = query.SearchString ?? "",
                Skip = query.Skip,
                Take = query.Take
            };

            if (query.FromDate.HasValue)
                request.FromDate = Timestamp.FromDateTime(DateTime.SpecifyKind(query.FromDate.Value, DateTimeKind.Utc));
            if (query.ToDate.HasValue)
                request.ToDate = Timestamp.FromDateTime(DateTime.SpecifyKind(query.ToDate.Value, DateTimeKind.Utc));

            var response = await _client.GetLogWindowsEventsAsync(request, cancellationToken: cancellationToken);
            var paging = BuildPaging(query.Skip, query.Take, response.Events.Count);

            return new PagedResult<WindowsEventDto>
            {
                Items = response.Events.Select(MapToWindowsEventDto).ToList(),
                TotalCount = response.TotalCount,
                Page = paging.Page,
                PageSize = paging.PageSize,
                HasMore = paging.HasMore
            };
        }

        public async Task<PagedResult<IISEventDto>> GetIISEventsAsync(IISEventQueryParams query, CancellationToken cancellationToken = default)
        {
            var request = new FilteredLogRequest
            {
                Token = _options.ApiKey,
                AccountId = query.AccountId,
                SearchString = query.SearchString ?? "",
                Skip = query.Skip,
                Take = query.Take
            };

            if (query.FromDate.HasValue)
                request.FromDate = Timestamp.FromDateTime(DateTime.SpecifyKind(query.FromDate.Value, DateTimeKind.Utc));
            if (query.ToDate.HasValue)
                request.ToDate = Timestamp.FromDateTime(DateTime.SpecifyKind(query.ToDate.Value, DateTimeKind.Utc));

            var response = await _client.GetLogIISEventsAsync(request, cancellationToken: cancellationToken);
            var paging = BuildPaging(query.Skip, query.Take, response.Events.Count);

            return new PagedResult<IISEventDto>
            {
                Items = response.Events.Select(MapToIISEventDto).ToList(),
                TotalCount = response.TotalCount,
                Page = paging.Page,
                PageSize = paging.PageSize,
                HasMore = paging.HasMore
            };
        }

        public async Task<PagedResult<WindowsMetricsDto>> GetWindowsMetricsAsync(WindowsMetricsQueryParams query, CancellationToken cancellationToken = default)
        {
            var request = new FilteredLogRequest
            {
                Token = _options.ApiKey,
                AccountId = query.AccountId,
                SearchString = query.SearchString ?? "",
                Skip = query.Skip,
                Take = query.Take
            };

            if (query.FromDate.HasValue)
                request.FromDate = Timestamp.FromDateTime(DateTime.SpecifyKind(query.FromDate.Value, DateTimeKind.Utc));
            if (query.ToDate.HasValue)
                request.ToDate = Timestamp.FromDateTime(DateTime.SpecifyKind(query.ToDate.Value, DateTimeKind.Utc));

            var response = await _client.GetLogWindowsMetricsAsync(request, cancellationToken: cancellationToken);
            var paging = BuildPaging(query.Skip, query.Take, response.Metrics.Count);

            return new PagedResult<WindowsMetricsDto>
            {
                Items = response.Metrics.Select(MapToWindowsMetricsDto).ToList(),
                TotalCount = response.TotalCount,
                Page = paging.Page,
                PageSize = paging.PageSize,
                HasMore = paging.HasMore
            };
        }

        #endregion

        private static (int Page, int PageSize, bool HasMore) BuildPaging(int skip, int take, int returnedCount)
        {
            if (take <= 0)
            {
                return (0, take, false);
            }

            return (skip / take, take, returnedCount == take);
        }

        #region Fluent Builders

        public ILogQueryBuilder QueryLogs() => new LogQueryBuilder(this);
        public ILogCacheQueryBuilder QueryCache() => new LogCacheQueryBuilder(this);
        [Obsolete("LogPoint fluent query APIs are coming soon and are currently disabled in the public SDK.")]
        public ILogPointQueryBuilder QueryLogPoints()
        {
            throw new NotSupportedException("QueryLogPoints() is marked [Soon] and is not available in this public SDK build yet.");
        }

        [Obsolete("LogRelation fluent query APIs are coming soon and are currently disabled in the public SDK.")]
        public ILogRelationQueryBuilder QueryRelations()
        {
            throw new NotSupportedException("QueryRelations() is marked [Soon] and is not available in this public SDK build yet.");
        }

        #endregion

        #region Mapping Methods

        private LogDto MapToLogDto(LogEntry entry)
        {
            return new LogDto
            {
                Id = entry.Id.ToString(),
                Guid = entry.Guid,
                Timestamp = entry.Timestamp?.ToDateTime() ?? DateTime.MinValue,
                Application = entry.Application,
                Environment = entry.Environment,
                Level = entry.Level,
                Message = entry.Message,
                Exception = entry.Exception,
                StackTrace = entry.StackTrace,
                Source = entry.Source,
                UserId = entry.UserId,
                UserEmail = entry.UserEmail,
                CorrelationId = entry.CorrelationId,
                RequestPath = entry.RequestPath,
                HttpMethod = entry.HttpMethod,
                AdditionalData = entry.AdditionalData,
                IpAddress = entry.IpAddress,
                StatusCode = entry.StatusCode,
                Description = entry.Description,
                Collection = entry.Collection,
                DateIn = entry.DateIn?.ToDateTime(),
                Labels = entry.Label.ToList(),
                AttributeS = entry.AttributeS.ToDictionary(kv => kv.Key, kv => kv.Value),
                AttributeN = entry.AttributeN.ToDictionary(kv => kv.Key, kv => kv.Value),
                AttributeB = entry.AttributeB.ToDictionary(kv => kv.Key, kv => kv.Value),
                AttributeD = entry.AttributeD.ToDictionary(kv => kv.Key, kv => kv.Value.ToDateTime())
            };
        }

        private LogCacheDto MapToLogCacheDto(LogCacheEntry entry)
        {
            var timestamp = entry.Timestamp?.ToDateTime() ?? DateTime.MinValue;
            return new LogCacheDto
            {
                Id = "", // grpc-server LogCacheEntry doesn't have Id field
                Key = entry.Key,
                Value = entry.Value,
                Timestamp = timestamp,
                Collection = "", // grpc-server LogCacheEntry doesn't have Collection field
                TtlSeconds = null,
                CreatedAt = timestamp,
                UpdatedAt = timestamp,
                Metadata = new List<LogCacheMetaDto>()
            };
        }

        private LogPointDto MapToLogPointDto(LogPointEntry entry)
        {
            return new LogPointDto
            {
                Id = "", // grpc-server LogPointEntry doesn't have Id field
                Guid = "", // grpc-server LogPointEntry doesn't have Guid field
                Measurement = entry.Measurement,
                Timestamp = entry.Timestamp?.ToDateTime() ?? DateTime.MinValue,
                Collection = entry.Collection,
                Tags = entry.Tags.Select(t => new LogPointMetaDto { Key = t.Key, Value = t.Value }).ToList(),
                Fields = entry.Fields.Select(f => new LogPointMetaDto { Key = f.Key, Value = f.Value }).ToList()
            };
        }

        private LogBeatDto MapToLogBeatDto(LogPointEntry entry)
        {
            // LogBeat uses LogPointEntry in grpc-server
            return new LogBeatDto
            {
                Id = "",
                Guid = "",
                Measurement = entry.Measurement,
                Timestamp = entry.Timestamp?.ToDateTime() ?? DateTime.MinValue,
                Collection = entry.Collection,
                Tags = entry.Tags.Select(t => new LogPointMetaDto { Key = t.Key, Value = t.Value }).ToList(),
                Fields = entry.Fields.Select(f => new LogPointMetaDto { Key = f.Key, Value = f.Value }).ToList()
            };
        }

        private LogRelationDto MapToLogRelationDto(LogRelationEntry entry)
        {
            return new LogRelationDto
            {
                Id = "", // grpc-server LogRelationEntry doesn't have Id field
                Guid = entry.Guid,
                Origin = entry.Origin,
                Relation = entry.Relation,
                Subject = entry.Subject,
                Collection = entry.Collection,
                DateIn = entry.DateIn?.ToDateTime(),
                OriginProperties = entry.OriginProperties.ToDictionary(kv => kv.Key, kv => kv.Value),
                SubjectProperties = entry.SubjectProperties.ToDictionary(kv => kv.Key, kv => kv.Value),
                RelationProperties = entry.RelationProperties.ToDictionary(kv => kv.Key, kv => kv.Value)
            };
        }

        private WindowsEventDto MapToWindowsEventDto(LogWindowsEventEntry entry)
        {
            return new WindowsEventDto
            {
                Id = entry.Id,
                TimeCreated = entry.TimeCreated?.ToDateTime() ?? DateTime.MinValue,
                ProviderName = entry.ProviderName,
                Channel = entry.Channel,
                Task = entry.Task,
                Opcode = entry.Opcode,
                Level = entry.Level,
                Keywords = entry.Keywords,
                Computer = entry.Computer,
                UserId = entry.UserId,
                Message = entry.Message,
                XmlData = entry.XmlData,
                AccountId = entry.AccountId,
                DateIn = entry.DateIn?.ToDateTime() ?? DateTime.MinValue,
                Collection = entry.Collection,
                IpAddress = entry.IpAddress
            };
        }

        private IISEventDto MapToIISEventDto(LogIISEventEntry entry)
        {
            return new IISEventDto
            {
                Id = entry.Id,
                Timestamp = entry.Timestamp?.ToDateTime() ?? DateTime.MinValue,
                Method = entry.Method,
                StatusCode = entry.StatusCode,
                UriStem = entry.UriStem,
                UriQuery = entry.UriQuery,
                ClientIp = entry.ClientIp,
                ServerIp = entry.ServerIp,
                ServerPort = entry.ServerPort,
                UserAgent = entry.UserAgent,
                TimeTaken = entry.TimeTaken,
                BytesSent = entry.BytesSent,
                BytesReceived = entry.BytesReceived,
                SiteName = entry.SiteName,
                Username = entry.Username,
                Referer = entry.Referer,
                Host = entry.Host,
                ProtocolVersion = entry.ProtocolVersion,
                SubStatus = entry.SubStatus,
                Win32Status = entry.Win32Status,
                SourceFile = entry.SourceFile,
                SiteId = entry.SiteId,
                Collection = entry.Collection,
                AccountId = entry.AccountId,
                DateIn = entry.DateIn?.ToDateTime() ?? DateTime.MinValue
            };
        }

        private WindowsMetricsDto MapToWindowsMetricsDto(LogWindowsMetricsEntry entry)
        {
            return new WindowsMetricsDto
            {
                Id = entry.Id,
                Timestamp = entry.Timestamp?.ToDateTime() ?? DateTime.MinValue,
                Measurement = entry.Measurement,
                ServerName = entry.ServerName,
                Environment = entry.Environment,
                CpuUsagePercent = entry.CpuUsagePercent,
                MemoryUsagePercent = entry.MemoryUsagePercent,
                DiskUsagePercent = entry.DiskUsagePercent,
                NetworkSpeedMbps = entry.NetworkSpeedMbps,
                Tags = entry.Tags,
                Fields = entry.Fields,
                AccountId = entry.AccountId,
                DateIn = entry.DateIn?.ToDateTime() ?? DateTime.MinValue,
                Collection = entry.Collection,
                Guid = entry.Guid
            };
        }

        #endregion

        #region Dispose

        public void Dispose()
        {
            DisposeAsync().AsTask().Wait();
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;

            try
            {
                _channel?.Dispose();
            }
            finally
            {
                _disposed = true;
            }
        }

        #endregion
    }
}
