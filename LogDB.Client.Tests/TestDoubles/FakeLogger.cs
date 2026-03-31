using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using com.logdb.LogDB;
using LogDB.Client.Models;
using LogDB.Extensions.Logging;

namespace LogDB.Client.Tests.TestDoubles;

internal class FakeLogger : ILogger
{
    public List<Log> SentLogs { get; } = new();

    public Task<LogResponseStatus> Log(Log logEntry)
    {
        SentLogs.Add(logEntry);
        return Task.FromResult(LogResponseStatus.Success);
    }

    public Task<LogResponseStatus> Log(LogBeat logEntry) => Task.FromResult(LogResponseStatus.Success);
    public Task<LogResponseStatus> Log(LogCache logEntry) => Task.FromResult(LogResponseStatus.Success);
    
    // Obsolete methods in ILogger
    public Task<LogResponseStatus> Log(LogPoint logEntry) => Task.FromResult(LogResponseStatus.Success);
    public Task<LogResponseStatus> Log(LogRelation logEntry) => Task.FromResult(LogResponseStatus.Success);

    public Task<LogResponseStatus> Log(IEnumerable<Log> logEntries) => Task.FromResult(LogResponseStatus.Success);
    public Task<LogResponseStatus> Log(IEnumerable<LogPoint> logEntries) => Task.FromResult(LogResponseStatus.Success);
    public Task<LogResponseStatus> Log(IEnumerable<LogBeat> logEntries) => Task.FromResult(LogResponseStatus.Success);
    public Task<LogResponseStatus> Log(IEnumerable<LogCache> logEntries) => Task.FromResult(LogResponseStatus.Success);
    public Task<LogResponseStatus> Log(IEnumerable<LogRelation> logEntries) => Task.FromResult(LogResponseStatus.Success);
    
    public com.logdb.LogDB.LogBuilders.LogEventBuilder Event() => com.logdb.LogDB.LogBuilders.LogEventBuilder.Create(this);
    public com.logdb.logger.LogBuilders.LogPointBuilder Point() => throw new NotImplementedException();
    public com.logdb.LogDB.LogBuilders.LogRelationBuilder Relation() => throw new NotImplementedException();

    public string ApiKey => "test-api-key";
}
