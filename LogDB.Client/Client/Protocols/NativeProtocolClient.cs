using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using com.logdb.grpc.logger;
using LogDB.Client.Models;
using LogDB.Client.Services;
using Google.Protobuf;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using static com.logdb.grpc.logger.LogGrpcService;

namespace LogDB.Extensions.Logging
{
    /// <summary>
    /// Native gRPC protocol client implementation
    /// </summary>
    internal class NativeProtocolClient : IProtocolClient, IDisposable
    {
        private readonly LogDBLoggerOptions _options;
        private readonly ILogger? _logger;
        private readonly LogGrpcServiceClient _client;
        private readonly GrpcChannel _channel;
        private readonly CompressService _compressService;
        private bool _disposed;

        public NativeProtocolClient(LogDBLoggerOptions options, ILogger? logger)
        {
            _options = options;
            _logger = logger;
            _compressService = new CompressService();

            // Get service URL
            var serviceUrl = GetServiceUrl();

            // Create gRPC channel
            var channelOptions = new GrpcChannelOptions
            {
                MaxReceiveMessageSize = 100 * 1024 * 1024, // 100MB
                MaxSendMessageSize = 100 * 1024 * 1024
            };

            // Only disable SSL validation if explicitly configured (for local development only)
#if NETFRAMEWORK
            // .NET Framework: try WinHttpHandler (HTTP/2) first, fall back to GrpcWebHandler (HTTP/1.1)
            // WinHttpHandler requires Windows 11 / Server 2022+; older OS needs GrpcWebHandler
            System.Net.Http.HttpMessageHandler handler;
            try
            {
                var winHttpHandler = new System.Net.Http.WinHttpHandler();
                if (_options.DangerouslyAcceptAnyServerCertificate)
                    winHttpHandler.ServerCertificateValidationCallback = (_, _, _, _) => true;
                // Test if WinHttpHandler can work on this OS by creating a temporary channel
                using var testChannel = GrpcChannel.ForAddress(serviceUrl, new GrpcChannelOptions { HttpHandler = winHttpHandler });
                handler = winHttpHandler;
            }
            catch
            {
                // WinHttpHandler failed (old Windows) — use gRPC-Web over HTTP/1.1
                var httpHandler = new System.Net.Http.HttpClientHandler();
                if (_options.DangerouslyAcceptAnyServerCertificate)
                    httpHandler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;
                handler = new global::Grpc.Net.Client.Web.GrpcWebHandler(global::Grpc.Net.Client.Web.GrpcWebMode.GrpcWeb, httpHandler);
            }
            channelOptions.HttpHandler = handler;
#else
            if (_options.DangerouslyAcceptAnyServerCertificate)
            {
                channelOptions.HttpHandler = new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback =
                        HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                };
            }
#endif

            _channel = GrpcChannel.ForAddress(serviceUrl, channelOptions);

            _client = new LogGrpcServiceClient(_channel);
        }

        private string GetServiceUrl()
        {
            if (!string.IsNullOrEmpty(_options.ServiceUrl))
            {
                return _options.ServiceUrl;
            }

            // Use discovery service
            try
            {
                var discoveryService = new DiscoveryService();
                var url = discoveryService.DiscoverServiceUrl("grpc-logger", _options.ApiKey);
                if (string.IsNullOrEmpty(url))
                {
                    throw new InvalidOperationException(
                        "Unable to discover LogDB gRPC endpoint. " +
                        "Please set ServiceUrl in options or configure LOGDB_GRPC_LOGGER_URL environment variable.");
                }
                _logger?.LogDebug("Discovered LogDB service at {Url}", url);
                return url;
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "Failed to discover LogDB gRPC endpoint. " +
                    "Please set ServiceUrl in options or configure LOGDB_GRPC_LOGGER_URL environment variable.", ex);
            }
        }



        public async Task<LogResponseStatus> SendLogAsync(Log log, CancellationToken cancellationToken = default)
        {
            try
            {
                var logRequest = log.ToGrpc();

                // Ensure ApiKey is set (protobuf MapField quirk: set it explicitly after mapping)
                if (string.IsNullOrEmpty(logRequest.ApiKey) && !string.IsNullOrEmpty(log.ApiKey))
                {
                    logRequest.ApiKey = log.ApiKey;
                }
                // Also set from options if log doesn't have it
                if (string.IsNullOrEmpty(logRequest.ApiKey) && !string.IsNullOrEmpty(_options.ApiKey))
                {
                    logRequest.ApiKey = _options.ApiKey;
                }

                // Create call options with explicit deadline (30 seconds) to prevent hanging
                var deadline = DateTime.UtcNow.AddSeconds(30);
                var callOptions = new CallOptions(deadline: deadline, cancellationToken: cancellationToken);

                if (_options.EnableCompression)
                {
                    var compressedPayload = _compressService.Compress(logRequest.ToByteArray());
                    var request = new CompressedPayload { CompressedData = ByteString.CopyFrom(compressedPayload) };
                    var response = await _client.SendCompressedLogAsync(request, callOptions);
                    return response.Status == "Success" ? LogResponseStatus.Success : LogResponseStatus.Failed;
                }
                else
                {
                    var response = await _client.LogAsync(logRequest, callOptions);
                    return response.Status == "Success" ? LogResponseStatus.Success : LogResponseStatus.Failed;
                }
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.Unauthenticated)
            {
                return LogResponseStatus.NotAuthorized;
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
            {
                // Distinguish between user cancellation and connection issues
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger?.LogDebug("Log send cancelled by user request");
                    throw; // Let it propagate as OperationCanceledException
                }
                // Connection was cancelled by server or network issue - retry is appropriate
                _logger?.LogWarning("gRPC call cancelled (connection issue): {Detail}", ex.Status.Detail);
                return LogResponseStatus.Failed;
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.DeadlineExceeded)
            {
                _logger?.LogWarning("gRPC call timed out after 30s");
                return LogResponseStatus.Failed;
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable)
            {
                _logger?.LogWarning("gRPC service unavailable: {Detail}", ex.Status.Detail);
                return LogResponseStatus.Failed;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to send log");
                return LogResponseStatus.Failed;
            }
        }

        public async Task<LogResponseStatus> SendLogPointAsync(LogPoint logPoint, CancellationToken cancellationToken = default)
        {
            try
            {
                var logRequest = logPoint.ToGrpc();

                if (_options.EnableCompression)
                {
                    var compressedPayload = _compressService.Compress(logRequest.ToByteArray());
                    var request = new CompressedPayload { CompressedData = ByteString.CopyFrom(compressedPayload) };
                    var response = await _client.SendCompressedLogPointAsync(request, cancellationToken: cancellationToken);
                    return response.Status == "Success" ? LogResponseStatus.Success : LogResponseStatus.Failed;
                }
                else
                {
                    var response = await _client.LogPointAsync(logRequest, cancellationToken: cancellationToken);
                    return response.Status == "Success" ? LogResponseStatus.Success : LogResponseStatus.Failed;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to send log point");
                return LogResponseStatus.Failed;
            }
        }

        public async Task<LogResponseStatus> SendLogBeatAsync(LogBeat logBeat, CancellationToken cancellationToken = default)
        {
            try
            {
                var logRequest = logBeat.ToGrpc();

                if (_options.EnableCompression)
                {
                    var compressedPayload = _compressService.Compress(logRequest.ToByteArray());
                    var request = new CompressedPayload { CompressedData = ByteString.CopyFrom(compressedPayload) };
                    var response = await _client.SendCompressedLogBeatAsync(request, cancellationToken: cancellationToken);
                    return response.Status == "Success" ? LogResponseStatus.Success : LogResponseStatus.Failed;
                }
                else
                {
                    var response = await _client.LogBeatAsync(logRequest, cancellationToken: cancellationToken);
                    return response.Status == "Success" ? LogResponseStatus.Success : LogResponseStatus.Failed;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to send log beat");
                return LogResponseStatus.Failed;
            }
        }

        public async Task<LogResponseStatus> SendLogCacheAsync(LogCache logCache, CancellationToken cancellationToken = default)
        {
            try
            {
                var logRequest = logCache.ToGrpc();

                if (_options.EnableCompression)
                {
                    var compressedPayload = _compressService.Compress(logRequest.ToByteArray());
                    var request = new CompressedPayload { CompressedData = ByteString.CopyFrom(compressedPayload) };
                    var response = await _client.SendCompressedLogCacheAsync(request, cancellationToken: cancellationToken);
                    return response.Status == "Success" ? LogResponseStatus.Success : LogResponseStatus.Failed;
                }
                else
                {
                    var response = await _client.LogCacheAsync(logRequest, cancellationToken: cancellationToken);
                    return response.Status == "Success" ? LogResponseStatus.Success : LogResponseStatus.Failed;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to send log cache");
                return LogResponseStatus.Failed;
            }
        }

        public async Task<LogResponseStatus> SendLogRelationAsync(LogRelation logRelation, CancellationToken cancellationToken = default)
        {
            try
            {
                var logRequest = logRelation.ToGrpc();

                if (_options.EnableCompression)
                {
                    var compressedPayload = _compressService.Compress(logRequest.ToByteArray());
                    var request = new CompressedPayload { CompressedData = ByteString.CopyFrom(compressedPayload) };
                    var response = await _client.SendCompressedLogRelationAsync(request, cancellationToken: cancellationToken);
                    return response.Status == "Success" ? LogResponseStatus.Success : LogResponseStatus.Failed;
                }
                else
                {
                    var response = await _client.LogRelationAsync(logRequest, cancellationToken: cancellationToken);
                    return response.Status == "Success" ? LogResponseStatus.Success : LogResponseStatus.Failed;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to send log relation");
                return LogResponseStatus.Failed;
            }
        }

        // Batch methods - using proper gRPC batch endpoints
        public async Task<LogResponseStatus> SendLogBatchAsync(IReadOnlyList<Log> logs, CancellationToken cancellationToken = default)
        {
            try
            {
                // Map logs to gRPC requests and ensure ApiKey is set
                var grpcLogs = logs.Select(log =>
                {
                    var grpcLog = log.ToGrpc();
                    if (string.IsNullOrEmpty(grpcLog.ApiKey) && !string.IsNullOrEmpty(log.ApiKey))
                    {
                        grpcLog.ApiKey = log.ApiKey;
                    }
                    // Also set from options if log doesn't have it
                    if (string.IsNullOrEmpty(grpcLog.ApiKey) && !string.IsNullOrEmpty(_options.ApiKey))
                    {
                        grpcLog.ApiKey = _options.ApiKey;
                    }
                    return grpcLog;
                }).ToList();

                // Create call options with explicit deadline (60 seconds for batch operations)
                var deadline = DateTime.UtcNow.AddSeconds(60);
                var callOptions = new CallOptions(deadline: deadline, cancellationToken: cancellationToken);

                if (_options.EnableCompression)
                {
                    // Create batch request
                    var batchRequest = new LogBatchRequest();
                    batchRequest.Logs.AddRange(grpcLogs);

                    // Compress the batch
                    var compressedPayload = _compressService.Compress(batchRequest.ToByteArray());
                    var request = new CompressedPayload { CompressedData = ByteString.CopyFrom(compressedPayload) };

                    var response = await _client.SendCompressedLogBatchAsync(request, callOptions);
                    return response.Status == "Success" ? LogResponseStatus.Success : LogResponseStatus.Failed;
                }
                else
                {
                    // Create batch request
                    var batchRequest = new LogBatchRequest();
                    batchRequest.Logs.AddRange(grpcLogs);

                    var response = await _client.LogBatchAsync(batchRequest, callOptions);
                    return response.Status == "Success" ? LogResponseStatus.Success : LogResponseStatus.Failed;
                }
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
            {
                // Distinguish between user cancellation and connection issues
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger?.LogDebug("Log batch send cancelled by user request");
                    throw; // Let it propagate as OperationCanceledException
                }
                // Connection was cancelled by server or network issue - retry is appropriate
                _logger?.LogWarning("gRPC batch call cancelled (connection issue): {Detail}", ex.Status.Detail);
                return LogResponseStatus.Failed;
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.DeadlineExceeded)
            {
                _logger?.LogWarning("gRPC batch call timed out after 60s (batch size: {Count})", logs.Count);
                return LogResponseStatus.Failed;
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable)
            {
                _logger?.LogError(ex, "Failed to send log batch via gRPC: {StatusCode} - {Detail}", ex.StatusCode, ex.Status.Detail);
                return LogResponseStatus.Failed;
            }
            catch (RpcException ex)
            {
                _logger?.LogError(ex, "Failed to send log batch via gRPC: {StatusCode} - {Detail}", ex.StatusCode, ex.Status.Detail);
                return LogResponseStatus.Failed;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to send log batch via gRPC");
                return LogResponseStatus.Failed;
            }
        }

        public async Task<LogResponseStatus> SendLogPointBatchAsync(IReadOnlyList<LogPoint> logPoints, CancellationToken cancellationToken = default)
        {
            try
            {
                var grpcLogPoints = logPoints.Select(lp => lp.ToGrpc()).ToList();

                if (_options.EnableCompression)
                {
                    // Create batch request
                    var batchRequest = new LogPointBatchRequest();
                    batchRequest.LogPoints.AddRange(grpcLogPoints);

                    // Compress the batch
                    var compressedPayload = _compressService.Compress(batchRequest.ToByteArray());
                    var request = new CompressedPayload { CompressedData = ByteString.CopyFrom(compressedPayload) };
                    
                    var response = await _client.SendCompressedLogPointBatchAsync(request, cancellationToken: cancellationToken);
                    return response.Status == "Success" ? LogResponseStatus.Success : LogResponseStatus.Failed;
                }
                else
                {
                    // Create batch request
                    var batchRequest = new LogPointBatchRequest();
                    batchRequest.LogPoints.AddRange(grpcLogPoints);

                    var response = await _client.LogPointBatchAsync(batchRequest, cancellationToken: cancellationToken);
                    return response.Status == "Success" ? LogResponseStatus.Success : LogResponseStatus.Failed;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to send log point batch via gRPC");
                return LogResponseStatus.Failed;
            }
        }

        public async Task<LogResponseStatus> SendLogBeatBatchAsync(IReadOnlyList<LogBeat> logBeats, CancellationToken cancellationToken = default)
        {
            try
            {
                var grpcLogBeats = logBeats.Select(lb => lb.ToGrpc()).ToList();

                if (_options.EnableCompression)
                {
                    // Create batch request (LogBeat uses LogPointBatchRequest)
                    var batchRequest = new LogPointBatchRequest();
                    batchRequest.LogPoints.AddRange(grpcLogBeats);

                    // Compress the batch
                    var compressedPayload = _compressService.Compress(batchRequest.ToByteArray());
                    var request = new CompressedPayload { CompressedData = ByteString.CopyFrom(compressedPayload) };
                    
                    var response = await _client.SendCompressedLogBeatBatchAsync(request, cancellationToken: cancellationToken);
                    return response.Status == "Success" ? LogResponseStatus.Success : LogResponseStatus.Failed;
                }
                else
                {
                    // Create batch request
                    var batchRequest = new LogPointBatchRequest();
                    batchRequest.LogPoints.AddRange(grpcLogBeats);

                    var response = await _client.LogBeatBatchAsync(batchRequest, cancellationToken: cancellationToken);
                    return response.Status == "Success" ? LogResponseStatus.Success : LogResponseStatus.Failed;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to send log beat batch via gRPC");
                return LogResponseStatus.Failed;
            }
        }

        public async Task<LogResponseStatus> SendLogCacheBatchAsync(IReadOnlyList<LogCache> logCaches, CancellationToken cancellationToken = default)
        {
            try
            {
                var grpcLogCaches = logCaches.Select(lc => lc.ToGrpc()).ToList();

                if (_options.EnableCompression)
                {
                    // Create batch request
                    var batchRequest = new LogCacheBatchRequest();
                    batchRequest.LogCaches.AddRange(grpcLogCaches);

                    // Compress the batch
                    var compressedPayload = _compressService.Compress(batchRequest.ToByteArray());
                    var request = new CompressedPayload { CompressedData = ByteString.CopyFrom(compressedPayload) };
                    
                    var response = await _client.SendCompressedLogCacheBatchAsync(request, cancellationToken: cancellationToken);
                    return response.Status == "Success" ? LogResponseStatus.Success : LogResponseStatus.Failed;
                }
                else
                {
                    // Create batch request
                    var batchRequest = new LogCacheBatchRequest();
                    batchRequest.LogCaches.AddRange(grpcLogCaches);

                    var response = await _client.LogCacheBatchAsync(batchRequest, cancellationToken: cancellationToken);
                    return response.Status == "Success" ? LogResponseStatus.Success : LogResponseStatus.Failed;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to send log cache batch via gRPC");
                return LogResponseStatus.Failed;
            }
        }

        public async Task<LogResponseStatus> SendLogRelationBatchAsync(IReadOnlyList<LogRelation> logRelations, CancellationToken cancellationToken = default)
        {
            try
            {
                var grpcLogRelations = logRelations.Select(lr => lr.ToGrpc()).ToList();

                if (_options.EnableCompression)
                {
                    // Create batch request
                    var batchRequest = new LogRelationBatchRequest();
                    batchRequest.LogRelations.AddRange(grpcLogRelations);

                    // Compress the batch
                    var compressedPayload = _compressService.Compress(batchRequest.ToByteArray());
                    var request = new CompressedPayload { CompressedData = ByteString.CopyFrom(compressedPayload) };
                    
                    var response = await _client.SendCompressedLogRelationBatchAsync(request, cancellationToken: cancellationToken);
                    return response.Status == "Success" ? LogResponseStatus.Success : LogResponseStatus.Failed;
                }
                else
                {
                    // Create batch request
                    var batchRequest = new LogRelationBatchRequest();
                    batchRequest.LogRelations.AddRange(grpcLogRelations);

                    var response = await _client.LogRelationBatchAsync(batchRequest, cancellationToken: cancellationToken);
                    return response.Status == "Success" ? LogResponseStatus.Success : LogResponseStatus.Failed;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to send log relation batch via gRPC");
                return LogResponseStatus.Failed;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            _channel?.Dispose();
            _disposed = true;
        }
    }
}
