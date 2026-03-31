using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using LogDB.Client.Tests.TestDoubles;
using LogDB.OpenTelemetry;
using OpenTelemetry;
using OpenTelemetry.Resources;
using Xunit;

namespace LogDB.Client.Tests.OpenTelemetry;

public sealed class LogDBTraceExporterTests
{
    [Fact]
    public void Export_MapsActivityToLog_AndSkipsRelationTrackMarkedSoon()
    {
        var options = new LogDBExporterOptions
        {
            ApiKey = "test-api-key",
            DefaultCollection = "traces",
            Resource = ResourceBuilder.CreateEmpty()
                .AddService("checkout-service")
                .AddAttributes(new[]
                {
                    new KeyValuePair<string, object>("deployment.environment", "development")
                })
                .Build()
        };

        var fakeClient = new FakeLogDBClient();
        var exporter = new LogDBTraceExporter(options, fakeClient);

        using var parent = new Activity("parent-span");
        parent.SetIdFormat(ActivityIdFormat.W3C);
        parent.Start();

        using var child = new Activity("child-span");
        child.SetIdFormat(ActivityIdFormat.W3C);
        child.SetParentId(parent.Id!);
        child.SetTag("http.method", "POST");
        child.SetTag("http.status_code", 201);
        child.AddEvent(new ActivityEvent(
            "db.query",
            default,
            new ActivityTagsCollection
            {
                { "db.system", "postgresql" }
            }));
        child.Start();
        child.SetStatus(ActivityStatusCode.Ok);
        child.Stop();
        parent.Stop();

        var batch = new Batch<Activity>(child);
        var result = exporter.Export(in batch);

        Assert.Equal(ExportResult.Success, result);
        Assert.Single(fakeClient.Logs);
        Assert.Empty(fakeClient.LogRelations);

        var exportedLog = fakeClient.Logs.Single();
        Assert.Equal("checkout-service", exportedLog.Application);
        Assert.Equal("development", exportedLog.Environment);
        Assert.Equal("traces", exportedLog.Collection);
        Assert.Equal(child.TraceId.ToString(), exportedLog.CorrelationId);
        Assert.Equal(child.SpanId.ToString(), exportedLog.AttributesS["span.id"]);
        Assert.Equal("POST", exportedLog.AttributesS["tag.http.method"]);
        Assert.Equal(201d, exportedLog.AttributesN["tag.http.status_code"]);
        Assert.Contains("event:db.query", exportedLog.Label);
        Assert.True(exportedLog.AttributesN["duration.ms"] >= 0);
        Assert.Equal(1d, exportedLog.AttributesN["span.events.count"]);

    }
}
