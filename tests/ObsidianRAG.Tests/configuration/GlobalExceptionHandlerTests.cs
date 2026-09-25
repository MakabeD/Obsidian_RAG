using System.Net;
using System.Net.Http.Json;
using chunker;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ObsidianRAG.Tests.program;
using Xunit;

namespace ObsidianRAG.Tests.configuration;

[Collection(nameof(ObsidianRAG.Tests.program.WebHost))]
public class GlobalExceptionHandlerTests
{
    private const string InternalMarker = "SECRET_INTERNAL_STATE_ABC123";

    [Fact]
    public async Task Unhandled_exception_returns_500_without_leaking_the_exception_message()
    {
        using var factory = CreateFactory(chroma: new ThrowingChroma(
            new InvalidOperationException(InternalMarker)));
        HttpClient client = factory.CreateClient();

        string sessionId = await CreateSessionAsync(client);
        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/session/{sessionId}/query", new { prompt = "hello", topK = (int?)null });

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(InternalMarker, body);
        Assert.Contains("An unexpected error occurred.", body);
    }

    [Fact]
    public async Task HttpRequestException_returns_502_with_a_generic_detail_and_keeps_the_upstream_message_in_logs_only()
    {
        var logs = new CapturingLoggerProvider();
        using var factory = CreateFactory(
            chroma: new ThrowingChroma(new HttpRequestException(InternalMarker)),
            logs: logs);
        HttpClient client = factory.CreateClient();

        string sessionId = await CreateSessionAsync(client);
        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/session/{sessionId}/query", new { prompt = "hello", topK = (int?)null });

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(InternalMarker, body);
        Assert.Contains("could not be reached", body);
        Assert.Contains(logs.Exceptions, e => e.Error.Message.Contains(InternalMarker));
    }

    private static WebApplicationFactory<Program> CreateFactory(IChromaService chroma, ILoggerProvider? logs = null)
        => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                if (logs is not null) services.AddSingleton<ILoggerProvider>(logs);
                RemoveAll(services, typeof(IEmbedder));
                RemoveAll(services, typeof(IChromaService));
                services.AddSingleton<IEmbedder>(new StubEmbedder());
                services.AddSingleton<IChromaService>(chroma);
            });
        });

    private static async Task<string> CreateSessionAsync(HttpClient client)
    {
        HttpResponseMessage created = await client.PostAsync("/session", content: null);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        SessionResponse? session = await created.Content.ReadFromJsonAsync<SessionResponse>();
        Assert.NotNull(session);
        return session!.SessionId;
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

    private sealed class StubEmbedder : IEmbedder
    {
        public float[] Embed(string text) => [1f, 0f];

        public IEnumerable<DocumentChunk> EmbeddRange(IEnumerable<DocumentChunk> documents) => documents;
    }

    private sealed class ThrowingChroma(Exception exception) : IChromaService
    {
        public Task InitializeAsync(CancellationToken ct = default) => Task.CompletedTask;

        public Task AddSessionRecordsAsync(string sessionId, List<ChromaDocument> documents, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task<List<SearchResult>> QuerySessionAsync(string sessionId, float[] queryEmbedding, int topK, CancellationToken ct = default)
            => throw exception;

        public Task TerminateSessionAsync(string sessionId, CancellationToken ct = default) => Task.CompletedTask;
    }
}
