using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ObsidianRAG.Tests.configuration;

public class RequestDiagnosticsEndpointTests
{
    [Fact]
    public async Task Handled_requests_are_recorded_and_served_from_diagnostics_endpoint()
    {
        using var factory = new WebApplicationFactory<Program>();
        HttpClient client = factory.CreateClient();

        HttpResponseMessage first = await client.GetAsync("/siu");
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        HttpResponseMessage diagnostics = await client.GetAsync("/diagnostics/requests");
        Assert.Equal(HttpStatusCode.OK, diagnostics.StatusCode);

        List<RecordedRequest>? recorded = await diagnostics.Content.ReadFromJsonAsync<List<RecordedRequest>>();
        Assert.NotNull(recorded);
        RecordedRequest? match = recorded!.Find(r => r.Path == "/siu");
        Assert.NotNull(match);
        Assert.Equal("GET", match!.Method);
        Assert.Equal(200, match.StatusCode);
    }

    private sealed record RecordedRequest(
        DateTime Timestamp, string RequestId, string Method, string Path, int StatusCode, long ElapsedMs);
}
