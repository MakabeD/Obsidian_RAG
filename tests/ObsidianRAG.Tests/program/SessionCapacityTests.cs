using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ObsidianRAG.Tests.program;

public class SessionCapacityTests
{
    [Fact]
    public async Task Post_session_returns_503_once_the_registry_is_full()
    {
        using WebApplicationFactory<Program> factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("Rag:MaxConcurrentSessions", "2");
                builder.ConfigureServices(services =>
                {
                    RemoveAll(services, typeof(IChromaService));
                    services.AddSingleton<IChromaService>(new StubChroma());
                });
            });
        HttpClient client = factory.CreateClient();

        HttpResponseMessage first = await client.PostAsync("/session", content: null);
        HttpResponseMessage second = await client.PostAsync("/session", content: null);
        HttpResponseMessage third = await client.PostAsync("/session", content: null);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, third.StatusCode);

        string body = await third.Content.ReadAsStringAsync();
        Assert.Contains("Session capacity reached", body);

        SessionResponse? firstSession = await first.Content.ReadFromJsonAsync<SessionResponse>();
        SessionResponse? secondSession = await second.Content.ReadFromJsonAsync<SessionResponse>();
        Assert.NotNull(firstSession);
        Assert.NotNull(secondSession);
        Assert.NotEqual(firstSession!.SessionId, secondSession!.SessionId);
    }

    [Fact]
    public async Task Deleting_a_session_frees_a_slot()
    {
        using WebApplicationFactory<Program> factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("Rag:MaxConcurrentSessions", "1");
                builder.ConfigureServices(services =>
                {
                    RemoveAll(services, typeof(IChromaService));
                    services.AddSingleton<IChromaService>(new StubChroma());
                });
            });
        HttpClient client = factory.CreateClient();

        HttpResponseMessage created = await client.PostAsync("/session", content: null);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        SessionResponse? session = await created.Content.ReadFromJsonAsync<SessionResponse>();
        Assert.NotNull(session);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.PostAsync("/session", content: null)).StatusCode);

        HttpResponseMessage deleted = await client.DeleteAsync($"/session/{session!.SessionId}");
        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);

        HttpResponseMessage recreated = await client.PostAsync("/session", content: null);
        Assert.Equal(HttpStatusCode.OK, recreated.StatusCode);
    }

    private static void RemoveAll(IServiceCollection services, Type t)
    {
        for (int i = services.Count - 1; i >= 0; i--)
        {
            if (services[i].ServiceType == t)
                services.RemoveAt(i);
        }
    }

    private sealed record SessionResponse(string SessionId);

    private sealed class StubChroma : IChromaService
    {
        public Task InitializeAsync(CancellationToken ct = default) => Task.CompletedTask;

        public Task AddSessionRecordsAsync(string sessionId, List<ChromaDocument> documents, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task<List<SearchResult>> QuerySessionAsync(string sessionId, float[] queryEmbedding, int topK, CancellationToken ct = default)
            => Task.FromResult(new List<SearchResult>());

        public Task TerminateSessionAsync(string sessionId, CancellationToken ct = default) => Task.CompletedTask;
    }
}
