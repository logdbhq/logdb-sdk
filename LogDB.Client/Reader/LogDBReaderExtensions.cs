using System;
using System.Threading.Tasks;
using LogDB.Client.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace LogDB.Extensions.Logging
{
    /// <summary>
    /// Extension methods for adding LogDB Reader to dependency injection
    /// </summary>
    public static class LogDBReaderExtensions
    {
        /// <summary>
        /// Adds LogDB Reader to the service collection
        /// </summary>
        /// <param name="services">The service collection</param>
        /// <param name="configure">Configuration action</param>
        /// <returns>The service collection</returns>
        public static IServiceCollection AddLogDBReader(this IServiceCollection services, Action<LogDBLoggerOptions> configure)
        {
            services.Configure(configure);
            services.TryAddSingleton<ILogDBReader, LogDBReader>();
            return services;
        }

        /// <summary>
        /// Adds LogDB Reader to the service collection with API key
        /// </summary>
        /// <param name="services">The service collection</param>
        /// <param name="apiKey">The API key</param>
        /// <param name="readerServiceUrl">Optional reader service URL</param>
        /// <returns>The service collection</returns>
        public static IServiceCollection AddLogDBReader(this IServiceCollection services, string apiKey, string? readerServiceUrl = null)
        {
            return services.AddLogDBReader(options =>
            {
                options.ApiKey = apiKey;
                if (!string.IsNullOrEmpty(readerServiceUrl))
                    options.ReaderServiceUrl = readerServiceUrl;
            });
        }

        /// <summary>
        /// Creates a standalone LogDB Reader instance
        /// </summary>
        /// <param name="apiKey">The API key</param>
        /// <param name="readerServiceUrl">Optional reader service URL</param>
        /// <returns>A new LogDB Reader instance</returns>
        public static ILogDBReader CreateReader(string apiKey, string? readerServiceUrl = null)
        {
            var options = Options.Create(new LogDBLoggerOptions
            {
                ApiKey = apiKey,
                ReaderServiceUrl = readerServiceUrl
            });

            return new LogDBReader(options);
        }

        /// <summary>
        /// Discovers the LogDB reader (grpc-server) URL via the SDK discovery service.
        /// Returns environment override fallback when discovery is unavailable.
        /// </summary>
        /// <returns>Discovered reader URL, or null when not available</returns>
        public static Task<string?> DiscoverReaderServiceUrlAsync()
        {
            var discoveryService = new DiscoveryService();
            return discoveryService.DiscoverServiceUrlAsync("grpc-server");
        }

        /// <summary>
        /// Synchronously discovers the LogDB reader (grpc-server) URL via the SDK discovery service.
        /// Returns environment override fallback when discovery is unavailable.
        /// </summary>
        /// <returns>Discovered reader URL, or null when not available</returns>
        public static string? DiscoverReaderServiceUrl()
        {
            var discoveryService = new DiscoveryService();
            return discoveryService.DiscoverServiceUrl("grpc-server");
        }
    }
}


