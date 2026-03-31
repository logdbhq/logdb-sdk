using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using LogDB.Client.Tests.TestDoubles;
using LogDB.OpenTelemetry;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using Xunit;

namespace LogDB.Client.Tests.OpenTelemetry;

public sealed class LogDBMetricExporterTests
{
    [Fact]
    public void Export_MapsMetricPointsToLogBeats()
    {
        var options = new LogDBExporterOptions
        {
            ApiKey = "test-api-key",
            DefaultCollection = "metrics",
            Resource = ResourceBuilder.CreateEmpty()
                .AddService("metrics-service")
                .AddAttributes(new[]
                {
                    new KeyValuePair<string, object>("deployment.environment", "development")
                })
                .Build()
        };

        var fakeClient = new FakeLogDBClient();
        var exporter = new LogDBMetricExporter(options, fakeClient);

        using var meter = new Meter("LogDB.Client.Tests.Metrics", "1.0.0");
        var requests = meter.CreateCounter<long>("sample_requests", unit: "requests");
        var durations = meter.CreateHistogram<double>("sample_duration_ms", unit: "ms");

        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddReader(new PeriodicExportingMetricReader(exporter, 60_000, 30_000))
            .Build();

        requests.Add(3, new KeyValuePair<string, object?>("endpoint", "/checkout"));
        durations.Record(128.5, new KeyValuePair<string, object?>("endpoint", "/checkout"));

        Assert.True(provider.ForceFlush(5_000));

        var counterBeat = fakeClient.LogBeats.FirstOrDefault(beat => beat.Measurement == "sample_requests");
        var histogramBeat = fakeClient.LogBeats.FirstOrDefault(beat => beat.Measurement == "sample_duration_ms");

        Assert.NotNull(counterBeat);
        Assert.NotNull(histogramBeat);

        Assert.Equal("metrics", counterBeat!.Collection);
        Assert.Equal("metrics-service", counterBeat.Application);
        Assert.Equal("development", counterBeat.Environment);
        Assert.Contains(counterBeat.Tag, tag => tag.Key == "metric.unit" && tag.Value == "requests");
        Assert.Contains(counterBeat.Tag, tag => tag.Key == "metric.type");
        Assert.Contains(counterBeat.Tag, tag => tag.Key == "metric.temporality");
        Assert.Contains(counterBeat.Tag, tag => tag.Key == "endpoint" && tag.Value == "/checkout");
        Assert.Contains(counterBeat.Field, field => field.Key == "value");
        Assert.True(counterBeat.Timestamp > DateTime.UnixEpoch);

        Assert.Contains(histogramBeat!.Field, field => field.Key == "count");
        Assert.Contains(histogramBeat.Field, field => field.Key == "sum");
    }
}
