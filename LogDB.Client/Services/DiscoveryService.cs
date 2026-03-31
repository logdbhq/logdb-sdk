using System.Collections.Concurrent;
using System.Net.Http;
using System.Text.Json;

namespace LogDB.Client.Services;

/// <summary>
/// Service discovery for LogDB endpoints
/// </summary>
internal class DiscoveryService
{
    private static readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(5)
    };

    private static readonly ConcurrentDictionary<string, (string? Url, DateTime CachedAt)> _discoveryCache = new();
    private static readonly TimeSpan CacheExpiry = TimeSpan.FromMinutes(5);

    public async Task<string?> DiscoverServiceUrlAsync(string serviceName, string? apiKey = null)
    {
        var cacheKey = $"{serviceName}:{apiKey ?? ""}";

        // Check cache first
        if (_discoveryCache.TryGetValue(cacheKey, out var cached) &&
            DateTime.UtcNow - cached.CachedAt < CacheExpiry)
        {
            return cached.Url;
        }

        var backendUrl = $"https://discovery.logdb.site/resolve/{serviceName}";

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, backendUrl);
            if (!string.IsNullOrEmpty(apiKey))
            {
                request.Headers.TryAddWithoutValidation("X-API-Key", apiKey);
            }

            var response = await _httpClient.SendAsync(request, cts.Token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

#if NETFRAMEWORK
            var responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
#else
            var responseContent = await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
#endif

            // Try parsing as ResolvedService JSON (from /resolve endpoint)
            string? discoveredUrl = null;
            try
            {
                using var doc = JsonDocument.Parse(responseContent);
                if (doc.RootElement.TryGetProperty("serviceUrl", out var serviceUrlProp))
                {
                    discoveredUrl = serviceUrlProp.GetString();
                }
            }
            catch
            {
                // Fallback: try as plain JSON string
                discoveredUrl = JsonSerializer.Deserialize<string>(responseContent);
            }

            if (!string.IsNullOrEmpty(discoveredUrl))
            {
                _discoveryCache[cacheKey] = (discoveredUrl, DateTime.UtcNow);
                return discoveredUrl;
            }
        }
        catch
        {
            // Suppress - fallback will be used
        }

        var fallbackUrl = GetFallbackServiceUrl(serviceName);
        if (fallbackUrl != null)
        {
            _discoveryCache[cacheKey] = (fallbackUrl, DateTime.UtcNow);
        }

        return fallbackUrl;
    }

    public string? DiscoverServiceUrl(string serviceName, string? apiKey = null)
    {
        var cacheKey = $"{serviceName}:{apiKey ?? ""}";

        // Check cache first
        if (_discoveryCache.TryGetValue(cacheKey, out var cached) &&
            DateTime.UtcNow - cached.CachedAt < CacheExpiry)
        {
            return cached.Url;
        }

        try
        {
            var task = DiscoverServiceUrlAsync(serviceName, apiKey);
            if (task.Wait(TimeSpan.FromSeconds(3)))
            {
                return task.Result;
            }
            return GetFallbackServiceUrl(serviceName);
        }
        catch
        {
            return GetFallbackServiceUrl(serviceName);
        }
    }

    private static string? GetFallbackServiceUrl(string serviceName)
    {
        // Allow environment variable override for local development/testing
        var envUrl = Environment.GetEnvironmentVariable($"LOGDB_{serviceName.ToUpperInvariant().Replace("-", "_")}_URL");
        return string.IsNullOrEmpty(envUrl) ? null : envUrl;
    }
}
