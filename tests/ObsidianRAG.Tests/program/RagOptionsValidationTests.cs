using configuration;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace ObsidianRAG.Tests.program;

public class RagOptionsValidationTests
{
    [Fact]
    public void Out_of_range_option_value_fails_host_startup()
    {
        Assert.ThrowsAny<OptionsValidationException>(() =>
        {
            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseSetting("Rag:MaxTopK", "0");
            });
            _ = factory.Services;
        });
    }

    [Fact]
    public void Empty_required_option_value_fails_host_startup()
    {
        Assert.ThrowsAny<OptionsValidationException>(() =>
        {
            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseSetting("Rag:CollectionName", "");
            });
            _ = factory.Services;
        });
    }

    [Fact]
    public void Default_options_pass_startup_validation()
    {
        using var factory = new WebApplicationFactory<Program>();
        Assert.Equal(50, factory.Services.GetRequiredService<IOptions<RagOptions>>().Value.MaxTopK);
    }
}
