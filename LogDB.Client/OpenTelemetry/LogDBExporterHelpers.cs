using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading.Tasks;
using LogDB.Client.Models;

namespace LogDB.OpenTelemetry
{
    internal static class LogDBExporterHelpers
    {
        internal static string ResolveCollection(LogDBExporterOptions options, string fallbackCollection)
        {
            return string.IsNullOrWhiteSpace(options.DefaultCollection)
                ? fallbackCollection
                : options.DefaultCollection!;
        }

        internal static bool TryWaitForStatus(
            Task<LogResponseStatus> task,
            LogDBExporterOptions options,
            string operation,
            out LogResponseStatus status)
        {
            status = LogResponseStatus.Failed;

            if (!TryWaitTaskInternal(task, options.ExporterTimeoutMilliseconds, options, operation))
            {
                return false;
            }

            try
            {
                status = task.GetAwaiter().GetResult();
                if (status == LogResponseStatus.Success)
                {
                    return true;
                }

                options.WriteDebug($"{operation} completed with status '{status}'.");
                return false;
            }
            catch (Exception ex)
            {
                options.ReportError(ex, operation);
                return false;
            }
        }

        internal static bool TryWait(
            Task task,
            int timeoutMilliseconds,
            LogDBExporterOptions options,
            string operation)
        {
            return TryWaitTaskInternal(task, timeoutMilliseconds, options, operation);
        }

        internal static void AddAttribute(Log log, string key, object? value)
        {
            if (string.IsNullOrWhiteSpace(key) || value == null)
            {
                return;
            }

            switch (value)
            {
                case string stringValue when !string.IsNullOrWhiteSpace(stringValue):
                    log.AttributesS[key] = stringValue;
                    break;
                case char charValue:
                    log.AttributesS[key] = charValue.ToString();
                    break;
                case bool boolValue:
                    log.AttributesB[key] = boolValue;
                    break;
                case sbyte sbyteValue:
                    log.AttributesN[key] = sbyteValue;
                    break;
                case byte byteValue:
                    log.AttributesN[key] = byteValue;
                    break;
                case short shortValue:
                    log.AttributesN[key] = shortValue;
                    break;
                case ushort ushortValue:
                    log.AttributesN[key] = ushortValue;
                    break;
                case int intValue:
                    log.AttributesN[key] = intValue;
                    break;
                case uint uintValue:
                    log.AttributesN[key] = uintValue;
                    break;
                case long longValue:
                    log.AttributesN[key] = longValue;
                    break;
                case ulong ulongValue:
                    log.AttributesN[key] = ulongValue;
                    break;
                case float floatValue:
                    log.AttributesN[key] = floatValue;
                    break;
                case double doubleValue:
                    log.AttributesN[key] = doubleValue;
                    break;
                case decimal decimalValue:
                    log.AttributesN[key] = (double)decimalValue;
                    break;
                case DateTime dateTimeValue:
                    log.AttributesD[key] = dateTimeValue.Kind == DateTimeKind.Utc
                        ? dateTimeValue
                        : dateTimeValue.ToUniversalTime();
                    break;
                case DateTimeOffset dateTimeOffsetValue:
                    log.AttributesD[key] = dateTimeOffsetValue.UtcDateTime;
                    break;
                default:
                    var converted = Convert.ToString(value, CultureInfo.InvariantCulture);
                    if (!string.IsNullOrWhiteSpace(converted))
                    {
                        log.AttributesS[key] = converted;
                    }
                    break;
            }
        }

        internal static void AddTag(List<LogMeta> tags, string key, string? value)
        {
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            tags.Add(new LogMeta
            {
                Key = key,
                Value = value
            });
        }

        internal static string BuildSpanEntityId(ActivityTraceId traceId, ActivitySpanId spanId)
        {
            return $"{traceId}:{spanId}";
        }

        private static bool TryWaitTaskInternal(
            Task task,
            int timeoutMilliseconds,
            LogDBExporterOptions options,
            string operation)
        {
            if (timeoutMilliseconds <= 0)
            {
                timeoutMilliseconds = 30000;
            }

            if (!task.IsCompleted)
            {
                var completed = task.Wait(timeoutMilliseconds);
                if (!completed)
                {
                    options.ReportError(
                        new TimeoutException($"{operation} exceeded timeout ({timeoutMilliseconds} ms)."),
                        operation);
                    return false;
                }
            }

            if (task.IsCanceled)
            {
                options.ReportError(new TaskCanceledException($"{operation} was canceled."), operation);
                return false;
            }

            try
            {
                task.GetAwaiter().GetResult();
                return true;
            }
            catch (Exception ex)
            {
                options.ReportError(ex, operation);
                return false;
            }
        }
    }
}
