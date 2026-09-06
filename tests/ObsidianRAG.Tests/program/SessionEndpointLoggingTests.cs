using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestPlatform.TestHost;
using Xunit;

namespace ObsidianRAG.Tests.program;

public class SessionEndpointLoggingTests
{
    [Fact]
    public async Task Successful_session_creation_does_not_emit_a_log_under_the_string_category_Endpoints()
    {
        var logs = new CapturingLoggerProvider();

        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<ILoggerProvider>(logs);
            });
        });

        HttpClient client = factory.CreateClient();
        HttpResponseMessage response = await client.PostAsync("/session", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.DoesNotContain(logs.Records, r => r.Category == "Endpoints");
    }
}
