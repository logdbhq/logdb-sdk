using System;
using NLog;

namespace LogDB.NLog.Examples
{
    /// <summary>
    /// Example demonstrating XML configuration for LogDB target
    /// </summary>
    public class XmlConfigExample
    {
        public static void Run()
        {
            // NLog will automatically load NLog.config if present
            // Or you can load a specific config file:
            // LogManager.Configuration = new XmlLoggingConfiguration("NLog.config");

            var logger = LogManager.GetCurrentClassLogger();

            // Use the logger - configuration comes from XML
            logger.Info("Application started");
            logger.Info("User {0} logged in", 12345);
            
            logger.Warn("This is a warning");
            
            try
            {
                throw new InvalidOperationException("Test exception");
            }
            catch (Exception ex)
            {
                logger.Error(ex, "An error occurred");
            }

            LogManager.Shutdown();
        }
    }
}






