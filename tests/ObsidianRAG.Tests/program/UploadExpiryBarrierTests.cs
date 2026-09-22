using System.Net;
using System.Net.Http.Json;
using System.Text;
using chunker;
using configuration;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace ObsidianRAG.Tests.program;

[Collection(nameof(WebHost))]
public class UploadExpiryBarrierTests
{
    [Fact]
    public async Task Upload_to_a_session_claimed_mid_request_is_rejected_before_any_chroma_write()
    {
        CountingChroma chroma = new();
        using WebApplicationFactory<Program> factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    TestSupport.RemoveAll(services, typeof(SessionRegistry));
                    TestSupport.RemoveAll(services, typeof(IEmbedder));
                    TestSupport.RemoveAll(services, typeof(IChromaService));
                    services.AddSingleton<IEmbedder>(new StubEmbedder());
                    services.AddSingleton<IChromaService>(chroma);
                    services.AddSingleton<SessionRegistry>(sp =>
                        new DyingMidRequestRegistry(sp.GetRequiredService<IOptions<RagOptions>>()));
                });
            });
        HttpClient client = factory.CreateClient();

        HttpResponseMessage created = await client.PostAsync("/session", content: null);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        TestSupport.SessionResponse? session = await created.Content.ReadFromJsonAsync<TestSupport.SessionResponse>();
        Assert.NotNull(session);

        using MultipartFormDataContent form = new();
        form.Add(
            new ByteArrayContent(Encoding.UTF8.GetBytes("# note\nbody")),
            "files",
            "note.md");

        HttpResponseMessage response = await client.PostAsync($"/session/{session!.SessionId}/md", form);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        Assert.Contains("expired during upload", body);
        Assert.Equal(0, chroma.AddCalls);
    }

    private sealed class DyingMidRequestRegistry(IOptions<RagOptions> options) : SessionRegistry(options)
    {
        private int _touches;

        public override bool Touch(string sessionId) => ++_touches <= 1;
    }

    private sealed class StubEmbedder : IEmbedder
    {
        public float[] Embed(string text) => [1f, 0f];

        public IEnumerable<DocumentChunk> EmbeddRange(IEnumerable<DocumentChunk> documents) => documents;
    }

    private sealed class CountingChroma : IChromaService
    {
        public int AddCalls;

        public Task InitializeAsync(CancellationToken ct = default) => Task.CompletedTask;

        public Task AddSessionRecordsAsync(string sessionId, List<ChromaDocument> documents, CancellationToken ct = default)
        {
            AddCalls++;
            return Task.CompletedTask;
        }

        public Task<List<SearchResult>> QuerySessionAsync(string sessionId, float[] queryEmbedding, int topK, CancellationToken ct = default)
            => Task.FromResult(new List<SearchResult>());

        public Task TerminateSessionAsync(string sessionId, CancellationToken ct = default) => Task.CompletedTask;
    }
}