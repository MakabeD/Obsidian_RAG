using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ObsidianRAG.Tests.configuration;

[Collection(nameof(ObsidianRAG.Tests.program.WebHost))]
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

    [Fact]
    public async Task Diagnostics_redact_live_session_ids_from_recorded_paths()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                ObsidianRAG.Tests.program.TestSupport.RemoveAll(services, typeof(IEmbedder));
                ObsidianRAG.Tests.program.TestSupport.RemoveAll(services, typeof(IChromaService));
                services.AddSingleton<IEmbedder>(new ObsidianRAG.Tests.program.TestSupport.StubEmbedder());
                services.AddSingleton<IChromaService>(new ObsidianRAG.Tests.program.TestSupport.StubChroma());
            });
        });
        HttpClient client = factory.CreateClient();

        HttpResponseMessage created = await client.PostAsync("/session", content: null);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        SessionResponse? session = await created.Content.ReadFromJsonAsync<SessionResponse>();
        Assert.NotNull(session);

        HttpResponseMessage query = await client.PostAsJsonAsync(
            $"/session/{session!.SessionId}/query",
            new { prompt = "", topK = (int?)null });
        Assert.Equal(HttpStatusCode.BadRequest, query.StatusCode);

        HttpResponseMessage diagnostics = await client.GetAsync("/diagnostics/requests");
        List<RecordedRequest>? recorded = await diagnostics.Content.ReadFromJsonAsync<List<RecordedRequest>>();
        Assert.NotNull(recorded);
        Assert.NotEmpty(recorded!);
        Assert.All(recorded!, r => Assert.DoesNotContain(session!.SessionId, r.Path));
    }

    private sealed record SessionResponse(string SessionId);

    private sealed record RecordedRequest(
        DateTime Timestamp, string RequestId, string Method, string Path, int StatusCode, long ElapsedMs);
}
