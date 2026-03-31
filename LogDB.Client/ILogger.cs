using System;
using LogDB.Client.Models;

namespace com.logdb.LogDB;

public interface ILogger
{
    Task<LogResponseStatus> Log(Log log);
    [Obsolete("LogPoint is coming soon and is currently disabled in the public SDK.")]
    Task<LogResponseStatus> Log(LogPoint log);
    Task<LogResponseStatus> Log(LogBeat log);
    Task<LogResponseStatus> Log(LogCache log);
    [Obsolete("LogRelation is coming soon and is currently disabled in the public SDK.")]
    Task<LogResponseStatus> Log(LogRelation log);

    Task<LogResponseStatus> Log(IEnumerable<Log> logEntries);
    [Obsolete("LogPoint batch writes are coming soon and are currently disabled in the public SDK.")]
    Task<LogResponseStatus> Log(IEnumerable<LogPoint> logEntries);
    Task<LogResponseStatus> Log(IEnumerable<LogBeat> logEntries);
    Task<LogResponseStatus> Log(IEnumerable<LogCache> logEntries);
    [Obsolete("LogRelation batch writes are coming soon and are currently disabled in the public SDK.")]
    Task<LogResponseStatus> Log(IEnumerable<LogRelation> logEntries);
}
