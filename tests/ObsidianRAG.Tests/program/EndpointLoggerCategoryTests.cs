using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace ObsidianRAG.Tests.program;

public class EndpointLoggerCategoryTests
{
    [Fact]
    public void Typed_endpoints_logger_emits_under_the_Endpoints_category_constant()
    {
        var logs = new CapturingLoggerProvider();

        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<ILoggerProvider>(logs);
            });
        });

        ILogger<Endpoints> logger = factory.Services.GetRequiredService<ILogger<Endpoints>>();
        logger.LogInformation("probe");

        Assert.Contains(logs.Records, r =>
            r.Category == "Endpoints"
            && r.Level == LogLevel.Information
            && r.Message == "probe");
    }
}
