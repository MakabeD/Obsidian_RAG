using configuration;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestPlatform.TestHost;
using Xunit;

namespace ObsidianRAG.Tests.program;

public class RagOptionsBindingTests
{
    [Fact]
    public void RagOptions_is_bound_from_configuration_so_consumers_see_the_configured_values()
    {
        const string expectedBaseUrl = "http://configured:1234";

        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Rag:ChromaBaseUrl"] = expectedBaseUrl,
                });
            });
        });

        IOptions<RagOptions> options = factory.Services.GetRequiredService<IOptions<RagOptions>>();

        Assert.Equal(expectedBaseUrl, options.Value.ChromaBaseUrl);
    }
}
