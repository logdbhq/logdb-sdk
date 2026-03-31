using System;
using LogDB.Client.Models;

namespace LogDB.Extensions.Logging.Enrichers
{
    /// <summary>
    /// Enriches logs with machine name information
    /// </summary>
    public class MachineNameEnricher : ILogEnricher
    {
        private readonly string _machineName;

        public MachineNameEnricher()
        {
            _machineName = Environment.MachineName;
        }

        public void Enrich(Log log)
        {
            log.AttributesS["machine.name"] = _machineName;
        }
    }
}

