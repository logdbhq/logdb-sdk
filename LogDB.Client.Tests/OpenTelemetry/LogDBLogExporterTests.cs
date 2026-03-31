using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using LogDB.Client.Tests.TestDoubles;
using LogDB.OpenTelemetry;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using Xunit;

namespace LogDB.Client.Tests.OpenTelemetry;

public sealed class LogDBLogExporterTests
{
    [Fact]
    public void Export_MapsStateScopesAndTraceCorrelation()
    {
        var options = new LogDBExporterOptions
        {
            ApiKey = "test-api-key",
            DefaultCollection = "logs",
            ExportProcessorType = ExportProcessorType.Simple,
            Resource = ResourceBuilder.CreateEmpty()
                .AddService("logging-service")
                .AddAttributes(new[]
                {
                    new KeyValuePair<string, object>("deployment.environment", "development")
                })
                .Build()
        };

        var fakeClient = new FakeLogDBClient();
        var exporter = new LogDBLogExporter(options, fakeClient);

        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddOpenTelemetry(logOptions =>
            {
                logOptions.IncludeScopes = true;
                logOptions.IncludeFormattedMessage = true;
                logOptions.ParseStateValues = true;
                logOptions.AddProcessor(new SimpleLogRecordExportProcessor(exporter));
            });
        });

        var logger = loggerFactory.CreateLogger("sample.logger");

        using (logger.BeginScope(new Dictionary<string, object?> { ["tenant"] = "acme" }))
        using (var activity = new Activity("sample-activity"))
        {
            activity.Start();
            logger.LogInformation("Processed order {OrderId}", "ORD-123");
            activity.Stop();
        }

        // Ensure OpenTelemetry provider flushes and exports before assertions.
        loggerFactory.Dispose();

        Assert.Single(fakeClient.Logs);

        var exportedLog = fakeClient.Logs.Single();
        Assert.Equal("logging-service", exportedLog.Application);
        Assert.Equal("development", exportedLog.Environment);
        Assert.Equal("logs", exportedLog.Collection);
        Assert.Equal("sample.logger", exportedLog.Source);
        Assert.Equal("ORD-123", exportedLog.AttributesS["OrderId"]);
        Assert.Equal("acme", exportedLog.AttributesS["scope.0.tenant"]);
        Assert.False(string.IsNullOrWhiteSpace(exportedLog.CorrelationId));
        Assert.True(exportedLog.AttributesS.ContainsKey("trace.id"));
        Assert.True(exportedLog.AttributesS.ContainsKey("span.id"));
    }
}
