using System.Threading;
using LogDB.Client.Models;

namespace LogDB.Extensions.Logging.Enrichers
{
    /// <summary>
    /// Enriches logs with thread information
    /// </summary>
    public class ThreadEnricher : ILogEnricher
    {
        public void Enrich(Log log)
        {
            var thread = Thread.CurrentThread;
            
            log.AttributesN["thread.id"] = thread.ManagedThreadId;
            
            if (!string.IsNullOrEmpty(thread.Name))
            {
                log.AttributesS["thread.name"] = thread.Name;
            }
            
            log.AttributesB["thread.background"] = thread.IsBackground;
            log.AttributesB["thread.pool"] = thread.IsThreadPoolThread;
        }
    }
}

