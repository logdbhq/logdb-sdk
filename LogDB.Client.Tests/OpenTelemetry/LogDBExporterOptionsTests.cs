using System.Collections.Generic;
using LogDB.Client.Tests.TestDoubles;
using LogDB.OpenTelemetry;
using OpenTelemetry.Resources;
using Xunit;

namespace LogDB.Client.Tests.OpenTelemetry;

public sealed class LogDBExporterOptionsTests
{
    [Fact]
    public void ResolveIdentity_UsesResourceServiceAndEnvironment_WhenAvailable()
    {
        var resource = ResourceBuilder.CreateEmpty()
            .AddService("orders-api")
            .AddAttributes(new[]
            {
                new KeyValuePair<string, object>("deployment.environment.name", "staging")
            })
            .Build();

        var options = new LogDBExporterOptions
        {
            ApiKey = "test-api-key",
            ServiceName = "fallback-service",
            DefaultEnvironment = "fallback-env",
            Resource = resource
        };

        var identity = options.ResolveIdentity(force: true);

        Assert.Equal("orders-api", identity.ServiceName);
        Assert.Equal("staging", identity.Environment);
        Assert.Equal("orders-api", options.ResolvedServiceName);
        Assert.Equal("staging", options.ResolvedEnvironment);
    }

    [Fact]
    public void ResolveIdentity_FallsBackToConfiguredAndEnvironmentValues()
    {
        using var scope = new EnvironmentVariableScope(
            ("LOGDB_DEFAULT_APPLICATION", "env-service"),
            ("LOGDB_DEFAULT_ENVIRONMENT", "env-development"));

        var options = new LogDBExporterOptions
        {
            ApiKey = "test-api-key"
        };

        var identity = options.ResolveIdentity(force: true);

        Assert.Equal("env-service", identity.ServiceName);
        Assert.Equal("env-development", identity.Environment);
    }

    [Fact]
    public void ResolveIdentity_FallsBackToProductionWhenEnvironmentMissing()
    {
        using var scope = new EnvironmentVariableScope(
            ("LOGDB_DEFAULT_APPLICATION", null),
            ("LOGDB_DEFAULT_ENVIRONMENT", null),
            ("OTEL_SERVICE_NAME", null),
            ("OTEL_RESOURCE_ATTRIBUTES", null));

        var options = new LogDBExporterOptions
        {
            ApiKey = "test-api-key",
            ServiceName = "configured-service"
        };

        var identity = options.ResolveIdentity(force: true);

        Assert.Equal("configured-service", identity.ServiceName);
        Assert.Equal("production", identity.Environment);
    }
}
