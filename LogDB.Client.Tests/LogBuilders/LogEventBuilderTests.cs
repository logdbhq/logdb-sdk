using System;
using System.Threading.Tasks;
using Xunit;
using com.logdb.LogDB.LogBuilders;
using LogDB.Client.Models;
using LogDB.Client.Tests.TestDoubles;

namespace LogDB.Client.Tests.LogBuilders;

public class LogEventBuilderTests
{
    [Fact]
    public async Task Builder_MapsStandardFieldsCorrectly()
    {
        var fakeLogger = new FakeLogger();
        
        LogEventBuilder.ApiKey = "global-test-key";
        LogEventBuilder.DefaultLogger = fakeLogger; // Override global for safety
        
        await LogEventBuilder.Create(fakeLogger)
            .SetMessage("Test message")
            .SetLogLevel(LogLevel.Warning)
            .SetApplication("MyApp")
            .SetEnvironment("Staging")
            .SetCorrelationId("corr-123")
            .SetUserEmail("test@test.com")
            .Log();

        Assert.Single(fakeLogger.SentLogs);
        var log = fakeLogger.SentLogs[0];
        
        Assert.Equal("Test message", log.Message);
        Assert.Equal(LogLevel.Warning, log.Level);
        Assert.Equal("MyApp", log.Application);
        Assert.Equal("Staging", log.Environment);
        Assert.Equal("corr-123", log.CorrelationId);
        Assert.Equal("test@test.com", log.UserEmail);
    }

    [Fact]
    public async Task Builder_AddsLabelsAndAttributes()
    {
        var fakeLogger = new FakeLogger();
        
        await LogEventBuilder.Create(fakeLogger)
            .SetMessage("Test")
            .AddLabel("auth")
            .AddLabel("login_failed")
            .AddAttribute("userId", "user-123")
            .AddAttribute("attempts", 3)
            .AddAttribute("isLocked", true)
            .Log();

        Assert.Single(fakeLogger.SentLogs);
        var log = fakeLogger.SentLogs[0];
        
        Assert.Contains("auth", log.Label);
        Assert.Contains("login_failed", log.Label);
        
        Assert.True(log.AttributesS.ContainsKey("userId"));
        Assert.Equal("user-123", log.AttributesS["userId"]);
        
        Assert.True(log.AttributesN.ContainsKey("attempts"));
        Assert.Equal(3, log.AttributesN["attempts"]);
        
        Assert.True(log.AttributesB.ContainsKey("isLocked"));
        Assert.True(log.AttributesB["isLocked"]);
    }

    [Fact]
    public async Task Builder_SetException_SetsCriticalAndSource()
    {
        var fakeLogger = new FakeLogger();
        
        var ex = new InvalidOperationException("Something went wrong");
        
        await LogEventBuilder.Create(fakeLogger)
            .SetException(ex)
            .Log();

        Assert.Single(fakeLogger.SentLogs);
        var log = fakeLogger.SentLogs[0];
        
        Assert.Equal(LogLevel.Exception, log.Level);
        Assert.Contains("Something went wrong", log.Message);
        Assert.NotNull(log.AdditionalData); // Stack trace or serialized exception
    }
}
