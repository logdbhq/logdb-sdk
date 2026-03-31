using System;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LogDB.Extensions.Logging
{
    /// <summary>
    /// Logger provider for LogDB
    /// </summary>
    [ProviderAlias("LogDB")]
    public class LogDBLoggerProvider : ILoggerProvider, ISupportExternalScope
    {
        private readonly IOptionsMonitor<LogDBLoggerOptions> _options;
        private readonly ILogDBClient _client;
        private readonly ConcurrentDictionary<string, LogDBLogger> _loggers = new();
        private IExternalScopeProvider _scopeProvider = NullExternalScopeProvider.Instance;
        private bool _disposed;

        public LogDBLoggerProvider(IOptionsMonitor<LogDBLoggerOptions> options, ILogDBClient client)
        {
            _options = options;
            _client = client;
        }

        public ILogger CreateLogger(string categoryName)
        {
            return _loggers.GetOrAdd(categoryName, name => 
                new LogDBLogger(name, _options.CurrentValue, _client, _scopeProvider));
        }

        public void SetScopeProvider(IExternalScopeProvider scopeProvider)
        {
            _scopeProvider = scopeProvider;
            
            // Update existing loggers with new scope provider
            foreach (var logger in _loggers.Values)
            {
                // This would require making scopeProvider settable in LogDBLogger
                // For now, new loggers will get the updated scope provider
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            try
            {
                // Flush any pending logs
                if (_client is IAsyncDisposable asyncDisposable)
                {
                    asyncDisposable.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(5));
                }
                else if (_client is IDisposable disposable)
                {
                    disposable.Dispose();
                }

                _loggers.Clear();
            }
            finally
            {
                _disposed = true;
            }
        }

        internal class NullExternalScopeProvider : IExternalScopeProvider
        {
            public static IExternalScopeProvider Instance { get; } = new NullExternalScopeProvider();

            private NullExternalScopeProvider() { }

            public void ForEachScope<TState>(Action<object?, TState> callback, TState state) { }

            public IDisposable Push(object? state) => NullScope.Instance;

            private class NullScope : IDisposable
            {
                public static NullScope Instance { get; } = new NullScope();
                public void Dispose() { }
            }
        }
    }
}

