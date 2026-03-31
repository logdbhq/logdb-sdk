using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using LogDB.Client.Models;
using LogDB.Extensions.Logging;

namespace LogDB.Extensions.Logging
{
    /// <summary>
    /// Main LogDB client implementation with batching, retries, and circuit breaker
    /// </summary>
    public class LogDBClient : ILogDBClient
    {
        private readonly LogDBLoggerOptions _options;
        private readonly ILogger<LogDBClient>? _logger;
        private readonly IAsyncPolicy<LogResponseStatus> _retryPolicy;
        private readonly IProtocolClient _protocolClient;
        private readonly Channel<LogEntry> _channel;
        private readonly Channel<bool> _flushChannel;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly Task _processingTask;
        private readonly SemaphoreSlim _flushSemaphore;
        private readonly object _lastBatchErrorLock = new();
        private Exception? _lastBatchError;
        private long _pendingEntries;
        private bool _disposed;

        public LogDBClient(IOptions<LogDBLoggerOptions> options, ILogger<LogDBClient>? logger = null)
            : this(options, null, logger)
        {
        }

        internal LogDBClient(IOptions<LogDBLoggerOptions> options, IProtocolClient? protocolClient, ILogger<LogDBClient>? logger = null)
        {
            _options = options.Value;
            _logger = logger;
            _flushSemaphore = new SemaphoreSlim(1, 1);
            _cancellationTokenSource = new CancellationTokenSource();

            // Configure retry policy with circuit breaker
            _retryPolicy = BuildRetryPolicy();

            // Create protocol-specific client
            _protocolClient = protocolClient ?? CreateProtocolClient();

            // Create channel for batching
            var channelOptions = new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false
            };
            _channel = Channel.CreateUnbounded<LogEntry>(channelOptions);
            _flushChannel = Channel.CreateUnbounded<bool>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false
            });

            // Start background processing
            _processingTask = ProcessLogsAsync(_cancellationTokenSource.Token);
        }

        public async Task<LogResponseStatus> LogAsync(Log log, CancellationToken cancellationToken = default)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(LogDBClient));

            log.ApiKey = _options.ApiKey;
            if (string.IsNullOrWhiteSpace(log.Collection))
                log.Collection = _options.DefaultCollection;
            if (string.IsNullOrWhiteSpace(log.Application) && !string.IsNullOrWhiteSpace(_options.DefaultApplication))
                log.Application = _options.DefaultApplication;
            if (string.IsNullOrWhiteSpace(log.Environment))
                log.Environment = _options.DefaultEnvironment;
            if (string.IsNullOrWhiteSpace(log.Guid))
                log.Guid = Guid.NewGuid().ToString();
            if (log.Timestamp == default)
                log.Timestamp = DateTime.UtcNow;

            if (_options.EnableBatching)
            {
                Interlocked.Increment(ref _pendingEntries);
                try
                {
                    var entry = new LogEntry { Type = LogEntryType.Log, Data = log };
                    await _channel.Writer.WriteAsync(entry, cancellationToken).ConfigureAwait(false);
                    return LogResponseStatus.Success;
                }
                catch (ChannelClosedException)
                {
                    Interlocked.Decrement(ref _pendingEntries);
                    // Channel was closed, fall back to direct send
                    _logger?.LogWarning("Channel closed, falling back to direct send");
                    return await _retryPolicy.ExecuteAsync(async () =>
                        await _protocolClient.SendLogAsync(log, cancellationToken).ConfigureAwait(false)
                    ).ConfigureAwait(false);
                }
                catch
                {
                    Interlocked.Decrement(ref _pendingEntries);
                    throw;
                }
            }

            return await _retryPolicy.ExecuteAsync(async () =>
                await _protocolClient.SendLogAsync(log, cancellationToken).ConfigureAwait(false)
            ).ConfigureAwait(false);
        }

        [Obsolete("LogPoint is coming soon and is currently disabled in the public SDK.")]
        public Task<LogResponseStatus> LogPointAsync(LogPoint logPoint, CancellationToken cancellationToken = default)
        {
            return Task.FromException<LogResponseStatus>(
                new NotSupportedException("LogPoint write APIs are marked [Soon] and are not available in this public SDK build yet."));
        }

        public async Task<LogResponseStatus> LogBeatAsync(LogBeat logBeat, CancellationToken cancellationToken = default)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(LogDBClient));

            logBeat.ApiKey = _options.ApiKey;
            if (string.IsNullOrWhiteSpace(logBeat.Collection))
                logBeat.Collection = _options.DefaultCollection;
            if (string.IsNullOrWhiteSpace(logBeat.Guid))
                logBeat.Guid = Guid.NewGuid().ToString();
            if (logBeat.Timestamp == default)
                logBeat.Timestamp = DateTime.UtcNow;

            if (!string.IsNullOrWhiteSpace(_options.DefaultApplication) &&
                !HasMeta(logBeat.Tag, "application"))
            {
                logBeat.Tag.Add(new LogMeta { Key = "application", Value = _options.DefaultApplication });
            }

            if (!HasMeta(logBeat.Tag, "environment"))
            {
                logBeat.Tag.Add(new LogMeta { Key = "environment", Value = _options.DefaultEnvironment });
            }

            if (_options.EnableBatching)
            {
                Interlocked.Increment(ref _pendingEntries);
                try
                {
                    var entry = new LogEntry { Type = LogEntryType.LogBeat, Data = logBeat };
                    await _channel.Writer.WriteAsync(entry, cancellationToken).ConfigureAwait(false);
                    return LogResponseStatus.Success;
                }
                catch (ChannelClosedException)
                {
                    Interlocked.Decrement(ref _pendingEntries);
                    _logger?.LogWarning("Channel closed, falling back to direct send");
                    return await _retryPolicy.ExecuteAsync(async () =>
                        await _protocolClient.SendLogBeatAsync(logBeat, cancellationToken).ConfigureAwait(false)
                    ).ConfigureAwait(false);
                }
                catch
                {
                    Interlocked.Decrement(ref _pendingEntries);
                    throw;
                }
            }

            return await _retryPolicy.ExecuteAsync(async () =>
                await _protocolClient.SendLogBeatAsync(logBeat, cancellationToken).ConfigureAwait(false)
            ).ConfigureAwait(false);
        }

        public async Task<LogResponseStatus> LogCacheAsync(LogCache logCache, CancellationToken cancellationToken = default)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(LogDBClient));

            logCache.ApiKey = _options.ApiKey;

            if (_options.EnableBatching)
            {
                Interlocked.Increment(ref _pendingEntries);
                try
                {
                    var entry = new LogEntry { Type = LogEntryType.LogCache, Data = logCache };
                    await _channel.Writer.WriteAsync(entry, cancellationToken).ConfigureAwait(false);
                    return LogResponseStatus.Success;
                }
                catch (ChannelClosedException)
                {
                    Interlocked.Decrement(ref _pendingEntries);
                    // Channel was closed, fall back to direct send
                    _logger?.LogWarning("Channel closed, falling back to direct send");
                }
                catch
                {
                    Interlocked.Decrement(ref _pendingEntries);
                    throw;
                }
            }

            return await _retryPolicy.ExecuteAsync(async () =>
                await _protocolClient.SendLogCacheAsync(logCache, cancellationToken).ConfigureAwait(false)
            ).ConfigureAwait(false);
        }

        [Obsolete("LogRelation is coming soon and is currently disabled in the public SDK.")]
        public Task<LogResponseStatus> LogRelationAsync(LogRelation logRelation, CancellationToken cancellationToken = default)
        {
            return Task.FromException<LogResponseStatus>(
                new NotSupportedException("LogRelation write APIs are marked [Soon] and are not available in this public SDK build yet."));
        }

        public async Task FlushAsync(CancellationToken cancellationToken = default)
        {
            if (_disposed) return;

            await _flushSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                // Signal processing loop to flush current partial buffers immediately.
                try
                {
                    await _flushChannel.Writer.WriteAsync(true, cancellationToken).ConfigureAwait(false);
                }
                catch (ChannelClosedException)
                {
                    // The client is shutting down; wait loop below will validate pending state.
                }

                while (Volatile.Read(ref _pendingEntries) > 0)
                {
                    if (_processingTask.IsCompleted)
                    {
                        await _processingTask.ConfigureAwait(false);
                        if (Volatile.Read(ref _pendingEntries) > 0)
                        {
                            throw new InvalidOperationException(
                                "Log processing stopped before all pending entries were flushed.");
                        }
                        break;
                    }

                    await Task.Delay(50, cancellationToken).ConfigureAwait(false);
                }

                var sendError = ConsumeLastBatchError();
                if (sendError != null)
                {
                    throw new InvalidOperationException(
                        "Flush completed but one or more LogDB batches failed to send. See inner exception for details.",
                        sendError);
                }
            }
            finally
            {
                _flushSemaphore.Release();
            }
        }

        public async Task<LogResponseStatus> SendLogBatchAsync(IReadOnlyList<Log> logs, CancellationToken cancellationToken = default)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(LogDBClient));

            if (_options.EnableBatching)
            {
                return await EnqueueBatchAsync(logs, LogEntryType.Log, cancellationToken).ConfigureAwait(false);
            }

            return await _retryPolicy.ExecuteAsync(async () =>
                await _protocolClient.SendLogBatchAsync(logs, cancellationToken).ConfigureAwait(false)
            ).ConfigureAwait(false);
        }

        [Obsolete("LogPoint batch writes are coming soon and are currently disabled in the public SDK.")]
        public Task<LogResponseStatus> SendLogPointBatchAsync(IReadOnlyList<LogPoint> logPoints, CancellationToken cancellationToken = default)
        {
            return Task.FromException<LogResponseStatus>(
                new NotSupportedException("LogPoint batch write APIs are marked [Soon] and are not available in this public SDK build yet."));
        }

        public async Task<LogResponseStatus> SendLogBeatBatchAsync(IReadOnlyList<LogBeat> logBeats, CancellationToken cancellationToken = default)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(LogDBClient));

            if (_options.EnableBatching)
            {
                return await EnqueueBatchAsync(logBeats, LogEntryType.LogBeat, cancellationToken).ConfigureAwait(false);
            }

            return await _retryPolicy.ExecuteAsync(async () =>
                await _protocolClient.SendLogBeatBatchAsync(logBeats, cancellationToken).ConfigureAwait(false)
            ).ConfigureAwait(false);
        }

        public async Task<LogResponseStatus> SendLogCacheBatchAsync(IReadOnlyList<LogCache> logCaches, CancellationToken cancellationToken = default)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(LogDBClient));

            if (_options.EnableBatching)
            {
                return await EnqueueBatchAsync(logCaches, LogEntryType.LogCache, cancellationToken).ConfigureAwait(false);
            }

            return await _retryPolicy.ExecuteAsync(async () =>
                await _protocolClient.SendLogCacheBatchAsync(logCaches, cancellationToken).ConfigureAwait(false)
            ).ConfigureAwait(false);
        }

        [Obsolete("LogRelation batch writes are coming soon and are currently disabled in the public SDK.")]
        public Task<LogResponseStatus> SendLogRelationBatchAsync(IReadOnlyList<LogRelation> logRelations, CancellationToken cancellationToken = default)
        {
            return Task.FromException<LogResponseStatus>(
                new NotSupportedException("LogRelation batch write APIs are marked [Soon] and are not available in this public SDK build yet."));
        }

        /// <summary>
        /// Routes a batch of items through the batching channel so that ProcessLogsAsync
        /// picks them up, _pendingEntries tracks them, and FlushAsync waits for them.
        /// The processing loop's drain phase grabs all available items immediately,
        /// so they get re-batched efficiently for a single gRPC call.
        /// </summary>
        private async Task<LogResponseStatus> EnqueueBatchAsync<T>(
            IReadOnlyList<T> items,
            LogEntryType entryType,
            CancellationToken cancellationToken) where T : notnull
        {
            Interlocked.Add(ref _pendingEntries, items.Count);
            var written = 0;
            try
            {
                foreach (var item in items)
                {
                    var entry = new LogEntry { Type = entryType, Data = item };
                    await _channel.Writer.WriteAsync(entry, cancellationToken).ConfigureAwait(false);
                    written++;
                }
                return LogResponseStatus.Success;
            }
            catch (ChannelClosedException)
            {
                // Channel closed during write — decrement unwritten items and fall back
                var unwritten = items.Count - written;
                Interlocked.Add(ref _pendingEntries, -unwritten);
                _logger?.LogWarning("Channel closed during batch enqueue, {Unwritten} items not queued", unwritten);
                return LogResponseStatus.Success; // already-written items will still be processed
            }
            catch
            {
                var unwritten = items.Count - written;
                Interlocked.Add(ref _pendingEntries, -unwritten);
                throw;
            }
        }

        private async Task ProcessLogsAsync(CancellationToken cancellationToken)
        {
            var buffer = new List<LogEntry>(_options.BatchSize);
            var forceFlush = false;

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    // Drain all currently available items and emit full batches immediately.
                    while (_channel.Reader.TryRead(out var entry))
                    {
                        buffer.Add(entry);

                        if (buffer.Count >= _options.BatchSize)
                        {
                            await SendBatchAsync(buffer, cancellationToken).ConfigureAwait(false);
                            buffer.Clear();
                        }
                    }

                    if (forceFlush && buffer.Count > 0)
                    {
                        await SendBatchAsync(buffer, cancellationToken).ConfigureAwait(false);
                        buffer.Clear();
                    }
                    forceFlush = false;

                    using var waitCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    var waitToken = waitCts.Token;

                    var readTask = _channel.Reader.WaitToReadAsync(waitToken).AsTask();
                    var flushTask = _flushChannel.Reader.WaitToReadAsync(waitToken).AsTask();
                    var timerTask = buffer.Count > 0
                        ? Task.Delay(_options.FlushInterval, waitToken)
                        : Task.Delay(Timeout.InfiniteTimeSpan, waitToken);

                    var shouldBreak = false;
                    var shouldFlush = false;
                    var completedTask = await Task.WhenAny(readTask, flushTask, timerTask).ConfigureAwait(false);

                    if (completedTask == readTask)
                    {
                        var canRead = await readTask.ConfigureAwait(false);
                        if (!canRead)
                        {
                            shouldBreak = true;
                        }
                    }
                    else if (completedTask == flushTask)
                    {
                        var hasFlushRequest = await flushTask.ConfigureAwait(false);
                        if (hasFlushRequest)
                        {
                            while (_flushChannel.Reader.TryRead(out _))
                            {
                                // Drain duplicate flush requests.
                            }

                            shouldFlush = true;
                        }
                    }
                    else if (completedTask == timerTask)
                    {
                        shouldFlush = true;
                    }

                    waitCts.Cancel();
                    await ObserveTaskAsync(readTask).ConfigureAwait(false);
                    await ObserveTaskAsync(flushTask).ConfigureAwait(false);
                    await ObserveTaskAsync(timerTask).ConfigureAwait(false);

                    if (shouldFlush)
                    {
                        forceFlush = true;
                    }

                    if (shouldBreak)
                    {
                        if (buffer.Count > 0)
                        {
                            await SendBatchAsync(buffer, cancellationToken).ConfigureAwait(false);
                            buffer.Clear();
                        }
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error in LogDB processing loop");
            }
            finally
            {
                // Send any remaining items
                if (buffer.Count > 0)
                {
                    await SendBatchAsync(buffer, CancellationToken.None).ConfigureAwait(false);
                }
            }
        }

        private static async Task ObserveTaskAsync(Task task)
        {
            try
            {
                await task.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected for the non-winning wait tasks each iteration.
            }
        }

        private async Task SendBatchAsync(List<LogEntry> entries, CancellationToken cancellationToken)
        {
            if (entries.Count == 0) return;

            try
            {
                // Group by type for efficient batch sending
                var groups = entries.GroupBy(e => e.Type);

                var tasks = new List<Task>();

                foreach (var group in groups)
                {
                    var task = group.Key switch
                    {
                        LogEntryType.Log => _retryPolicy.ExecuteAsync(async () =>
                        {
                            var logs = group.Select(e => (Log)e.Data).ToList();
                            // Ensure ApiKey is set on all logs in batch (safety check)
                            foreach (var log in logs)
                            {
                                if (string.IsNullOrEmpty(log.ApiKey))
                                {
                                    log.ApiKey = _options.ApiKey;
                                    _logger?.LogWarning("ApiKey was missing on log in batch, set from options");
                                }
                            }
                            await _protocolClient.SendLogBatchAsync(logs, cancellationToken).ConfigureAwait(false);
                            return LogResponseStatus.Success;
                        }),
                        LogEntryType.LogPoint => _retryPolicy.ExecuteAsync(async () =>
                        {
                            var logPoints = group.Select(e => (LogPoint)e.Data).ToList();
                            await _protocolClient.SendLogPointBatchAsync(logPoints, cancellationToken).ConfigureAwait(false);
                            return LogResponseStatus.Success;
                        }),
                        LogEntryType.LogBeat => _retryPolicy.ExecuteAsync(async () =>
                        {
                            var logBeats = group.Select(e => (LogBeat)e.Data).ToList();
                            await _protocolClient.SendLogBeatBatchAsync(logBeats, cancellationToken).ConfigureAwait(false);
                            return LogResponseStatus.Success;
                        }),
                        LogEntryType.LogCache => _retryPolicy.ExecuteAsync(async () =>
                        {
                            var logCaches = group.Select(e => (LogCache)e.Data).ToList();
                            await _protocolClient.SendLogCacheBatchAsync(logCaches, cancellationToken).ConfigureAwait(false);
                            return LogResponseStatus.Success;
                        }),
                        LogEntryType.LogRelation => _retryPolicy.ExecuteAsync(async () =>
                        {
                            var logRelations = group.Select(e => (LogRelation)e.Data).ToList();
                            await _protocolClient.SendLogRelationBatchAsync(logRelations, cancellationToken).ConfigureAwait(false);
                            return LogResponseStatus.Success;
                        }),
                        _ => Task.FromResult(LogResponseStatus.Success)
                    };

                    tasks.Add(task);
                }

                await Task.WhenAll(tasks).ConfigureAwait(false);
                ClearLastBatchError();

                // Success: decrement pending count for all entries
                var remaining = Interlocked.Add(ref _pendingEntries, -entries.Count);
                if (remaining < 0)
                {
                    Interlocked.Exchange(ref _pendingEntries, 0);
                }
            }
            catch (Exception ex)
            {
                RecordLastBatchError(ex);
                _logger?.LogError(ex, "Failed to send batch of {Count} entries, starting retry triage", entries.Count);

                // Partition entries: retryable (re-queue) vs exhausted (individual fallback)
                var exhausted = new List<LogEntry>();

                foreach (var entry in entries)
                {
                    if (entry.BatchRetryCount < _options.MaxBatchRetries)
                    {
                        entry.BatchRetryCount++;
                        if (_channel.Writer.TryWrite(entry))
                        {
                            // Re-queued successfully — _pendingEntries stays incremented
                            continue;
                        }
                        // Channel is closed/completed — treat as exhausted
                    }
                    exhausted.Add(entry);
                }

                var requeued = entries.Count - exhausted.Count;
                if (requeued > 0)
                {
                    _logger?.LogWarning(
                        "Re-queued {RequeueCount} entries for batch retry (attempt {Attempt}/{Max})",
                        requeued, entries[0].BatchRetryCount, _options.MaxBatchRetries);
                }

                // Fallback: send exhausted entries individually via the proven direct path
                foreach (var entry in exhausted)
                {
                    try
                    {
                        await SendIndividualFallbackAsync(entry, cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception individualEx)
                    {
                        _logger?.LogError(individualEx,
                            "Individual fallback also failed for {Type} entry. Entry dropped.", entry.Type);

                        if (_options.OnError != null && entry.Type == LogEntryType.Log)
                        {
                            _options.OnError(individualEx, new List<Log> { (Log)entry.Data });
                        }
                    }
                    finally
                    {
                        Interlocked.Decrement(ref _pendingEntries);
                    }
                }
            }
        }

        private async Task SendIndividualFallbackAsync(LogEntry entry, CancellationToken cancellationToken)
        {
            await _retryPolicy.ExecuteAsync(async () =>
            {
                return entry.Type switch
                {
                    LogEntryType.Log => await _protocolClient.SendLogAsync(
                        (Log)entry.Data, cancellationToken).ConfigureAwait(false),
                    LogEntryType.LogBeat => await _protocolClient.SendLogBeatAsync(
                        (LogBeat)entry.Data, cancellationToken).ConfigureAwait(false),
                    LogEntryType.LogCache => await _protocolClient.SendLogCacheAsync(
                        (LogCache)entry.Data, cancellationToken).ConfigureAwait(false),
                    LogEntryType.LogPoint => await _protocolClient.SendLogPointAsync(
                        (LogPoint)entry.Data, cancellationToken).ConfigureAwait(false),
                    LogEntryType.LogRelation => await _protocolClient.SendLogRelationAsync(
                        (LogRelation)entry.Data, cancellationToken).ConfigureAwait(false),
                    _ => LogResponseStatus.Success
                };
            }).ConfigureAwait(false);
        }

        private static bool HasMeta(IEnumerable<LogMeta> items, string key)
        {
            return items.Any(x => x.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
        }

        private void RecordLastBatchError(Exception ex)
        {
            lock (_lastBatchErrorLock)
            {
                _lastBatchError = ex;
            }
        }

        private void ClearLastBatchError()
        {
            lock (_lastBatchErrorLock)
            {
                _lastBatchError = null;
            }
        }

        private Exception? ConsumeLastBatchError()
        {
            lock (_lastBatchErrorLock)
            {
                var ex = _lastBatchError;
                _lastBatchError = null;
                return ex;
            }
        }



        private IAsyncPolicy<LogResponseStatus> BuildRetryPolicy()
        {
            var retryPolicy = Policy<LogResponseStatus>
                .HandleResult(r => r != LogResponseStatus.Success)
                .Or<Exception>()
                .WaitAndRetryAsync(
                    _options.MaxRetries,
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(_options.RetryBackoffMultiplier, retryAttempt - 1) * _options.RetryDelay.TotalSeconds),
                    onRetry: (outcome, timespan, retryCount, context) =>
                    {
                        _logger?.LogWarning("Retry {RetryCount} after {TimeSpan}ms", retryCount, timespan.TotalMilliseconds);
                    });

            if (_options.EnableCircuitBreaker)
            {
                var circuitBreakerPolicy = Policy<LogResponseStatus>
                    .HandleResult(r => r != LogResponseStatus.Success)
                    .Or<Exception>()
                    .CircuitBreakerAsync(
                        handledEventsAllowedBeforeBreaking: (int)(_options.CircuitBreakerFailureThreshold * 100),
                        durationOfBreak: _options.CircuitBreakerDurationOfBreak,
                        onBreak: (result, duration) =>
                        {
                            _logger?.LogWarning("Circuit breaker opened for {Duration}s", duration.TotalSeconds);
                        },
                        onReset: () =>
                        {
                            _logger?.LogInformation("Circuit breaker reset");
                        });

                return Policy.WrapAsync(retryPolicy, circuitBreakerPolicy);
            }

            return retryPolicy;
        }

        private IProtocolClient CreateProtocolClient()
        {
            return _options.Protocol switch
            {
                LogDBProtocol.Native => new NativeProtocolClient(_options, _logger),
                LogDBProtocol.OpenTelemetry => new OpenTelemetryProtocolClient(_options, _logger),
                LogDBProtocol.Rest => new RestProtocolClient(_options, _logger),
                _ => throw new NotSupportedException($"Protocol {_options.Protocol} is not supported")
            };
        }

        public void Dispose()
        {
            DisposeAsync().AsTask().GetAwaiter().GetResult();
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;

            try
            {
                // Complete the channel
                _channel.Writer.TryComplete();
                _flushChannel.Writer.TryComplete();

                // Wait for processing to complete
                await _processingTask.ConfigureAwait(false);

                // Dispose protocol client
                if (_protocolClient is IDisposable disposable)
                {
                    disposable.Dispose();
                }

                _cancellationTokenSource.Dispose();
                _flushSemaphore.Dispose();
            }
            finally
            {
                _disposed = true;
            }
        }

        private enum LogEntryType
        {
            Log,
            LogPoint,
            LogBeat,
            LogCache,
            LogRelation
        }

        private class LogEntry
        {
            public LogEntryType Type { get; set; }
            public object Data { get; set; } = null!;
            public int BatchRetryCount { get; set; }
        }
    }
}
