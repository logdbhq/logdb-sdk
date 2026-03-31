using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using com.logdb.logger;
using LogDB.Client.Models;
using LogDB.Client.Tests.TestDoubles;

namespace LogDB.Client.Tests.LoggerFacade;

public class LoggerTests
{
    [Fact]
    public async Task SoonApis_ThrowNotSupported()
    {
        using var env = new EnvironmentVariableScope(("LOGDB_GRPC_LOGGER_URL", "https://localhost:5001"));
        var logger = await Logger.CreateAsync("test-key", options => options.ServiceUrl = "https://localhost:5001");

        Assert.Throws<NotSupportedException>(() => logger.Point());
        Assert.Throws<NotSupportedException>(() => logger.Relation());
        await Assert.ThrowsAsync<NotSupportedException>(() => logger.Log(new LogPoint()));
        await Assert.ThrowsAsync<NotSupportedException>(() => logger.Log(new LogRelation()));
        await Assert.ThrowsAsync<NotSupportedException>(() => logger.Log(new[] { new LogPoint() }));
        await Assert.ThrowsAsync<NotSupportedException>(() => logger.Log(new[] { new LogRelation() }));
    }

    [Fact]
    public async Task EmptySupportedBatches_ReturnSuccessWithoutSending()
    {
        using var env = new EnvironmentVariableScope(("LOGDB_GRPC_LOGGER_URL", "https://localhost:5001"));
        var logger = await Logger.CreateAsync("test-key", options => options.ServiceUrl = "https://localhost:5001");

        Assert.Equal(LogResponseStatus.Success, await logger.Log(new List<Log>()));
        Assert.Equal(LogResponseStatus.Success, await logger.Log(new List<LogBeat>()));
        Assert.Equal(LogResponseStatus.Success, await logger.Log(new List<LogCache>()));
    }
}
