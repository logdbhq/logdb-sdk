using System;
using System.Threading.Tasks;
using Xunit;
using LogDB.Client.Services;
using LogDB.Client.Tests.TestDoubles;

namespace LogDB.Client.Tests.Services;

public class DiscoveryServiceTests
{
    [Fact]
    public async Task Discover_FallbackToEnvironmentVariable_WhenNetworkFailsOrNotPresent()
    {
        // Use a unique service name to avoid static cache collisions across tests
        var serviceName = Guid.NewGuid().ToString(); 
        var envVarName = $"LOGDB_{serviceName.ToUpperInvariant().Replace("-", "_")}_URL";
        var expectedUrl = "https://test-fallback-url:5001";

        using var envScope = new EnvironmentVariableScope((envVarName, expectedUrl));
        var discoveryService = new DiscoveryService();

        // This will naturally fail the real HTTP call (since the random GUID service doesn't exist on Discovery API),
        // and hit the fallback logic checking the environment variable.
        var resultUrl = await discoveryService.DiscoverServiceUrlAsync(serviceName, "test-api-key");

        Assert.Equal(expectedUrl, resultUrl);
    }
    
    [Fact]
    public void DiscoverSync_FallbackToEnvironmentVariable()
    {
        var serviceName = Guid.NewGuid().ToString(); 
        var envVarName = $"LOGDB_{serviceName.ToUpperInvariant().Replace("-", "_")}_URL";
        var expectedUrl = "https://sync-test-url:5001";

        using var envScope = new EnvironmentVariableScope((envVarName, expectedUrl));
        var discoveryService = new DiscoveryService();

        var resultUrl = discoveryService.DiscoverServiceUrl(serviceName, "test-api-key");

        Assert.Equal(expectedUrl, resultUrl);
    }
}
