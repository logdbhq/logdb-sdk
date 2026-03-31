using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Xunit;
using LogDB.Client.Models;
using LogDB.Extensions.Logging;
using LogDB.Client.Tests.TestDoubles;

namespace LogDB.Client.Tests.Client;

public class LogDBClientTests
{
    private LogDBLoggerOptions CreateOptions(int batchSize = 10)
    {
        return new LogDBLoggerOptions
        {
            ApiKey = "test-key",
            EnableBatching = true,
            BatchSize = batchSize,
            FlushInterval = TimeSpan.FromSeconds(5),
            MaxRetries = 0,
            EnableCircuitBreaker = false,
            RetryDelay = TimeSpan.Zero
        };
    }

    [Fact]
    public async Task LogAsync_EnableBatchingTrue_BatchesLogs()
    {
        var options = CreateOptions(batchSize: 5);
        var protocolClient = new FakeProtocolClient();
        
        await using var client = new LogDBClient(Options.Create(options), protocolClient);

        // Send 4 logs, should not trigger batch flush yet
        for (int i = 0; i < 4; i++)
        {
            await client.LogAsync(new Log { Message = $"Msg {i}" });
        }
        
        // Give background thread a tiny moment, though it shouldn't flush
        await Task.Delay(50);
        Assert.Empty(protocolClient.SentLogs);

        // Send 5th log, should trigger flush (since batch size is 5)
        await client.LogAsync(new Log { Message = "Msg 4" });

        // Wait a tiny bit for the channel reader to process the batch
        await Task.Delay(200);

        Assert.Equal(5, protocolClient.SentLogs.Count);
        Assert.Equal(1, protocolClient.SendLogBatchCallCount);
    }

    [Fact]
    public async Task FlushAsync_DrainsAllPendingLogs()
    {
        var options = CreateOptions(batchSize: 100);
        var protocolClient = new FakeProtocolClient();
        
        await using var client = new LogDBClient(Options.Create(options), protocolClient);

        // Send 3 logs (below batch size)
        for (int i = 0; i < 3; i++)
        {
            await client.LogAsync(new Log { Message = $"Msg {i}" });
        }

        // Force a flush
        await client.FlushAsync();

        Assert.Equal(3, protocolClient.SentLogs.Count);
        Assert.Equal(1, protocolClient.SendLogBatchCallCount);
    }
    
    [Fact]
    public async Task DisposeAsync_FlushesPendingLogs()
    {
        var options = CreateOptions(batchSize: 100);
        var protocolClient = new FakeProtocolClient();
        
        var client = new LogDBClient(Options.Create(options), protocolClient);

        await client.LogAsync(new Log { Message = "Pending log" });
        
        // Dispose should flush the channel
        await client.DisposeAsync();
        
        Assert.Single(protocolClient.SentLogs);
    }
    
    [Fact]
    public async Task DefaultsAreApplied_WhenMissingFromLog()
    {
        var options = CreateOptions();
        options.DefaultApplication = "TestApp";
        options.DefaultEnvironment = "Staging";
        options.DefaultCollection = "custom-logs";
        options.EnableBatching = false; // Direct send to avoid racing in test
        
        var protocolClient = new FakeProtocolClient();
        await using var client = new LogDBClient(Options.Create(options), protocolClient);

        var log = new Log { Message = "Test defaults" };
        await client.LogAsync(log);

        Assert.Single(protocolClient.SentLogs);
        var sentLog = protocolClient.SentLogs[0];
        
        Assert.Equal("test-key", sentLog.ApiKey);
        Assert.Equal("TestApp", sentLog.Application);
        Assert.Equal("Staging", sentLog.Environment);
        Assert.Equal("custom-logs", sentLog.Collection);
        Assert.NotNull(sentLog.Guid);
        Assert.NotEqual(default, sentLog.Timestamp);
    }

    [Fact]
    public async Task LogBeatAsync_AppliesDefaultsAndMeta_WhenMissingFromBeat()
    {
        var options = CreateOptions();
        options.DefaultApplication = "BeatApp";
        options.DefaultEnvironment = "Production";
        options.DefaultCollection = "beats";
        options.EnableBatching = false;

        var protocolClient = new FakeProtocolClient();
        await using var client = new LogDBClient(Options.Create(options), protocolClient);

        var beat = new LogBeat { Measurement = "heartbeat" };
        await client.LogBeatAsync(beat);

        Assert.Single(protocolClient.SentLogBeats);
        var sentBeat = protocolClient.SentLogBeats[0];

        Assert.Equal("test-key", sentBeat.ApiKey);
        Assert.Equal("beats", sentBeat.Collection);
        Assert.NotNull(sentBeat.Guid);
        Assert.NotEqual(default, sentBeat.Timestamp);
        Assert.Contains(sentBeat.Tag, tag => tag.Key == "application" && tag.Value == "BeatApp");
        Assert.Contains(sentBeat.Tag, tag => tag.Key == "environment" && tag.Value == "Production");
    }

    [Fact]
    public async Task LogCacheAsync_SetsApiKeyBeforeSending()
    {
        var options = CreateOptions();
        options.EnableBatching = false;

        var protocolClient = new FakeProtocolClient();
        await using var client = new LogDBClient(Options.Create(options), protocolClient);

        var cache = new LogCache { Key = "session:1", Value = "value" };
        await client.LogCacheAsync(cache);

        Assert.Single(protocolClient.SentLogCaches);
        Assert.Equal("test-key", protocolClient.SentLogCaches[0].ApiKey);
    }

    [Fact]
    public async Task SoonApis_ThrowNotSupported()
    {
        var options = CreateOptions();
        var protocolClient = new FakeProtocolClient();
        await using var client = new LogDBClient(Options.Create(options), protocolClient);

        await Assert.ThrowsAsync<NotSupportedException>(() => client.LogPointAsync(new LogPoint()));
        await Assert.ThrowsAsync<NotSupportedException>(() => client.LogRelationAsync(new LogRelation()));
        await Assert.ThrowsAsync<NotSupportedException>(() => client.SendLogPointBatchAsync(new[] { new LogPoint() }));
        await Assert.ThrowsAsync<NotSupportedException>(() => client.SendLogRelationBatchAsync(new[] { new LogRelation() }));
    }

    [Fact]
    public async Task FlushAsync_RequeuesFailedBatch_ThenSucceedsOnRetry()
    {
        var options = CreateOptions(batchSize: 2);
        options.MaxBatchRetries = 1;

        var protocolClient = new FakeProtocolClient();
        protocolClient.SendLogBatchExceptions.Enqueue(new InvalidOperationException("batch failed"));

        await using var client = new LogDBClient(Options.Create(options), protocolClient);

        await client.LogAsync(new Log { Message = "Msg 1" });
        await client.LogAsync(new Log { Message = "Msg 2" });

        await client.FlushAsync();

        Assert.Equal(2, protocolClient.SentLogs.Count);
        Assert.Equal(2, protocolClient.SendLogBatchCallCount);
        Assert.Equal(0, protocolClient.SendLogCallCount);
    }

    [Fact]
    public async Task FlushAsync_WhenBatchRetriesAreExhausted_FallsBackToIndividualSendsAndThrows()
    {
        var options = CreateOptions(batchSize: 2);
        options.MaxBatchRetries = 0;

        var protocolClient = new FakeProtocolClient();
        var batchFailure = new InvalidOperationException("batch failed");
        protocolClient.SendLogBatchExceptions.Enqueue(batchFailure);

        await using var client = new LogDBClient(Options.Create(options), protocolClient);

        await client.LogAsync(new Log { Message = "Msg 1" });
        await client.LogAsync(new Log { Message = "Msg 2" });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => client.FlushAsync());

        Assert.Same(batchFailure, ex.InnerException);
        Assert.Equal(2, protocolClient.SentLogs.Count);
        Assert.Equal(1, protocolClient.SendLogBatchCallCount);
        Assert.Equal(2, protocolClient.SendLogCallCount);
    }

    [Fact]
    public async Task FlushAsync_WhenIndividualFallbackFails_InvokesOnErrorForEachLog()
    {
        var options = CreateOptions(batchSize: 2);
        options.MaxBatchRetries = 0;

        var protocolClient = new FakeProtocolClient();
        var batchFailure = new InvalidOperationException("batch failed");
        protocolClient.SendLogBatchExceptions.Enqueue(batchFailure);
        protocolClient.SendLogExceptions.Enqueue(new InvalidOperationException("log 1 failed"));
        protocolClient.SendLogExceptions.Enqueue(new InvalidOperationException("log 2 failed"));

        var onErrorCallCount = 0;
        options.OnError = (_, logs) =>
        {
            onErrorCallCount++;
            Assert.Single(logs);
        };

        await using var client = new LogDBClient(Options.Create(options), protocolClient);

        await client.LogAsync(new Log { Message = "Msg 1" });
        await client.LogAsync(new Log { Message = "Msg 2" });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => client.FlushAsync());

        Assert.Same(batchFailure, ex.InnerException);
        Assert.Empty(protocolClient.SentLogs);
        Assert.Equal(1, protocolClient.SendLogBatchCallCount);
        Assert.Equal(2, protocolClient.SendLogCallCount);
        Assert.Equal(2, onErrorCallCount);
    }
}
