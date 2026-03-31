using System;
using System.Reflection;
using System.Runtime.InteropServices;
using LogDB.Client.Models;

namespace LogDB.Extensions.Logging.Enrichers
{
    /// <summary>
    /// Enriches logs with environment information
    /// </summary>
    public class EnvironmentEnricher : ILogEnricher
    {
        private readonly string _processName;
        private readonly int _processId;
        private readonly string _osDescription;
        private readonly string _frameworkDescription;
        private readonly string? _appVersion;

        public EnvironmentEnricher()
        {
            var process = System.Diagnostics.Process.GetCurrentProcess();
            _processName = process.ProcessName;
            _processId = process.Id;
            _osDescription = RuntimeInformation.OSDescription;
            _frameworkDescription = RuntimeInformation.FrameworkDescription;
            
            // Try to get application version
            try
            {
                _appVersion = Assembly.GetEntryAssembly()?.GetName().Version?.ToString();
            }
            catch
            {
                _appVersion = null;
            }
        }

        public void Enrich(Log log)
        {
            log.AttributesS["process.name"] = _processName;
            log.AttributesN["process.id"] = _processId;
            log.AttributesS["os.description"] = _osDescription;
            log.AttributesS["framework.description"] = _frameworkDescription;
            
            if (!string.IsNullOrEmpty(_appVersion))
            {
                log.AttributesS["app.version"] = _appVersion;
            }
        }
    }
}

