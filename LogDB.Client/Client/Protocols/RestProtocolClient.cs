using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using LogDB.Client.Models;
using LogDB.Client.Services;

namespace LogDB.Extensions.Logging
{
    /// <summary>
    /// REST API protocol client implementation
    /// </summary>
    internal class RestProtocolClient : IProtocolClient, IDisposable
    {
        private readonly LogDBLoggerOptions _options;
        private readonly ILogger? _logger;
        private readonly HttpClient _httpClient;
        private bool _disposed;

        public RestProtocolClient(LogDBLoggerOptions options, ILogger? logger)
        {
            _options = options;
            _logger = logger;

            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(GetRestEndpoint()),
                Timeout = _options.RequestTimeout
            };

            // Add headers
            foreach (var header in _options.Headers)
            {
                _httpClient.DefaultRequestHeaders.Add(header.Key, header.Value);
            }

            // Ensure API key is present for REST authentication unless explicitly provided by caller.
            if (!_options.Headers.Keys.Any(k => k.Equals("X-LogDB-ApiKey", StringComparison.OrdinalIgnoreCase)) &&
                !string.IsNullOrWhiteSpace(_options.ApiKey))
            {
                _httpClient.DefaultRequestHeaders.Add("X-LogDB-ApiKey", _options.ApiKey);
            }
        }

        private string GetRestEndpoint()
        {
            if (!string.IsNullOrEmpty(_options.ServiceUrl))
            {
                return _options.ServiceUrl;
            }

            // Try to discover REST API
            try
            {
                var discoveryService = new DiscoveryService();
                var url = discoveryService.DiscoverServiceUrl("rest-api", _options.ApiKey);
                if (string.IsNullOrEmpty(url))
                {
                    throw new InvalidOperationException(
                        "Unable to discover LogDB REST API endpoint. " +
                        "Please set ServiceUrl in options or configure LOGDB_REST_API_URL environment variable.");
                }
                _logger?.LogDebug("Discovered LogDB REST API at {Url}", url);
                return url;
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "Failed to discover LogDB REST API endpoint. " +
                    "Please set ServiceUrl in options or configure LOGDB_REST_API_URL environment variable.", ex);
            }
        }

        public async Task<LogResponseStatus> SendLogAsync(Log log, CancellationToken cancellationToken = default)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("/api/log/event", log, cancellationToken);
                
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<LogResponse?>(cancellationToken: cancellationToken);
                    return result?.Status ?? LogResponseStatus.Failed;
                }

                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    return LogResponseStatus.NotAuthorized;
                }

                _logger?.LogWarning("REST API request failed with status {StatusCode}", response.StatusCode);
                return LogResponseStatus.Failed;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to send log via REST API");
                return LogResponseStatus.Failed;
            }
        }

        public async Task<LogResponseStatus> SendLogPointAsync(LogPoint logPoint, CancellationToken cancellationToken = default)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("/api/log/point", logPoint, cancellationToken);
                
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<LogResponse?>(cancellationToken: cancellationToken);
                    return result?.Status ?? LogResponseStatus.Failed;
                }

                return LogResponseStatus.Failed;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to send log point via REST API");
                return LogResponseStatus.Failed;
            }
        }

        public async Task<LogResponseStatus> SendLogBeatAsync(LogBeat logBeat, CancellationToken cancellationToken = default)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("/api/log/beat", logBeat, cancellationToken);
                
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<LogResponse?>(cancellationToken: cancellationToken);
                    return result?.Status ?? LogResponseStatus.Failed;
                }

                return LogResponseStatus.Failed;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to send log beat via REST API");
                return LogResponseStatus.Failed;
            }
        }

        public async Task<LogResponseStatus> SendLogCacheAsync(LogCache logCache, CancellationToken cancellationToken = default)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("/api/log/cache", logCache, cancellationToken);
                
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<LogResponse?>(cancellationToken: cancellationToken);
                    return result?.Status ?? LogResponseStatus.Failed;
                }

                return LogResponseStatus.Failed;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to send log cache via REST API");
                return LogResponseStatus.Failed;
            }
        }

        public async Task<LogResponseStatus> SendLogRelationAsync(LogRelation logRelation, CancellationToken cancellationToken = default)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("/api/log/relation", logRelation, cancellationToken);
                
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<LogResponse?>(cancellationToken: cancellationToken);
                    return result?.Status ?? LogResponseStatus.Failed;
                }

                return LogResponseStatus.Failed;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to send log relation via REST API");
                return LogResponseStatus.Failed;
            }
        }

        // Batch methods - send individual calls in parallel
        public async Task<LogResponseStatus> SendLogBatchAsync(IReadOnlyList<Log> logs, CancellationToken cancellationToken = default)
        {
            try
            {
                // Send individual calls instead of batch
                var tasks = logs.Select(log => SendLogAsync(log, cancellationToken));
                var results = await Task.WhenAll(tasks);
                return results.All(r => r == LogResponseStatus.Success) ? LogResponseStatus.Success : LogResponseStatus.Failed;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to send log batch via REST API");
                return LogResponseStatus.Failed;
            }
        }

        public async Task<LogResponseStatus> SendLogPointBatchAsync(IReadOnlyList<LogPoint> logPoints, CancellationToken cancellationToken = default)
        {
            try
            {
                // Send individual calls instead of batch
                var tasks = logPoints.Select(lp => SendLogPointAsync(lp, cancellationToken));
                var results = await Task.WhenAll(tasks);
                return results.All(r => r == LogResponseStatus.Success) ? LogResponseStatus.Success : LogResponseStatus.Failed;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to send log point batch via REST API");
                return LogResponseStatus.Failed;
            }
        }

        public async Task<LogResponseStatus> SendLogBeatBatchAsync(IReadOnlyList<LogBeat> logBeats, CancellationToken cancellationToken = default)
        {
            try
            {
                // Send individual calls instead of batch
                var tasks = logBeats.Select(lb => SendLogBeatAsync(lb, cancellationToken));
                var results = await Task.WhenAll(tasks);
                return results.All(r => r == LogResponseStatus.Success) ? LogResponseStatus.Success : LogResponseStatus.Failed;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to send log beat batch via REST API");
                return LogResponseStatus.Failed;
            }
        }

        public async Task<LogResponseStatus> SendLogCacheBatchAsync(IReadOnlyList<LogCache> logCaches, CancellationToken cancellationToken = default)
        {
            try
            {
                // Send individual calls instead of batch
                var tasks = logCaches.Select(lc => SendLogCacheAsync(lc, cancellationToken));
                var results = await Task.WhenAll(tasks);
                return results.All(r => r == LogResponseStatus.Success) ? LogResponseStatus.Success : LogResponseStatus.Failed;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to send log cache batch via REST API");
                return LogResponseStatus.Failed;
            }
        }

        public async Task<LogResponseStatus> SendLogRelationBatchAsync(IReadOnlyList<LogRelation> logRelations, CancellationToken cancellationToken = default)
        {
            try
            {
                // Send individual calls instead of batch
                var tasks = logRelations.Select(lr => SendLogRelationAsync(lr, cancellationToken));
                var results = await Task.WhenAll(tasks);
                return results.All(r => r == LogResponseStatus.Success) ? LogResponseStatus.Success : LogResponseStatus.Failed;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to send log relation batch via REST API");
                return LogResponseStatus.Failed;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            _httpClient?.Dispose();
            _disposed = true;
        }
    }
}
