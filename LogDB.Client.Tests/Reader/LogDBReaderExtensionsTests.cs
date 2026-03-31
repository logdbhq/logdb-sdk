using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;
using LogDB.Extensions.Logging;

namespace LogDB.Client.Tests.Reader;

public class LogDBReaderExtensionsTests
{
    [Fact]
    public void AddLogDBReader_WithApiKey_RegistersReaderAndConfiguresOptions()
    {
        var services = new ServiceCollection();

        services.AddLogDBReader("test-key", "https://reader.example");

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<LogDBLoggerOptions>>().Value;

        Assert.Equal("test-key", options.ApiKey);
        Assert.Equal("https://reader.example", options.ReaderServiceUrl);
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(ILogDBReader) &&
                                                descriptor.ImplementationType == typeof(LogDBReader) &&
                                                descriptor.Lifetime == ServiceLifetime.Singleton);
    }
}
