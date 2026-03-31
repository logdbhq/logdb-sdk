using System;
using NLog;
using NLog.Config;

namespace LogDB.NLog
{
    /// <summary>
    /// Extension methods for configuring LogDB target in NLog
    /// </summary>
    public static class LogDBTargetExtensions
    {
        /// <summary>
        /// Add LogDB target to logging configuration
        /// </summary>
        public static LoggingConfiguration AddLogDBTarget(
            this LoggingConfiguration config,
            string targetName,
            string apiKey,
            LogDBPayloadType defaultPayloadType,
            Action<LogDBTarget>? configure = null)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));
            if (string.IsNullOrWhiteSpace(targetName))
                throw new ArgumentException("Target name is required", nameof(targetName));
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new ArgumentException("API key is required", nameof(apiKey));

            var target = new LogDBTarget
            {
                ApiKey = apiKey,
                DefaultPayloadType = defaultPayloadType,
                Name = targetName
            };

            configure?.Invoke(target);

            config.AddTarget(targetName, target);
            return config;
        }

        /// <summary>
        /// Add LogDB target with default rule
        /// </summary>
        public static LoggingConfiguration AddLogDBTargetWithRule(
            this LoggingConfiguration config,
            string targetName,
            string apiKey,
            LogDBPayloadType defaultPayloadType,
            global::NLog.LogLevel? minLevel = null,
            global::NLog.LogLevel? maxLevel = null,
            Action<LogDBTarget>? configure = null)
        {
            config.AddLogDBTarget(targetName, apiKey, defaultPayloadType, configure);
            config.AddRule(minLevel ?? global::NLog.LogLevel.Info, maxLevel ?? global::NLog.LogLevel.Fatal, targetName);
            return config;
        }

        /// <summary>
        /// Create LogDB target with options
        /// </summary>
        public static LogDBTarget CreateLogDBTarget(
            string apiKey,
            LogDBPayloadType defaultPayloadType,
            Action<LogDBTarget>? configure = null)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new ArgumentException("API key is required", nameof(apiKey));

            var target = new LogDBTarget
            {
                ApiKey = apiKey,
                DefaultPayloadType = defaultPayloadType
            };

            configure?.Invoke(target);
            return target;
        }
    }
}






