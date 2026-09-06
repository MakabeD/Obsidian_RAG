using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace ObsidianRAG.Tests.program;

public class EndpointLoggerCategoryTests
{
    [Fact]
    public void Typed_endpoints_logger_resolves_and_emits_under_the_marker_category()
    {
        var logs = new CapturingLoggerProvider();

        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<ILoggerProvider>(logs);
            });
        });

        ILogger<EndpointsMarker> logger = factory.Services.GetRequiredService<ILogger<EndpointsMarker>>();
        logger.LogInformation("probe");

        Assert.Contains(logs.Records, r =>
            r.Category == typeof(EndpointsMarker).FullName
            && r.Level == LogLevel.Information
            && r.Message == "probe");
    }
}
