using System.Net;
using System.Net.Http.Json;
using System.Text;
using chunker;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ObsidianRAG.Tests.program;

[Collection(nameof(WebHost))]
public class ChunkCountCapTests
{
    [Fact]
    public async Task Upload_producing_more_chunks_than_the_cap_is_rejected_before_any_embedding()
    {
        CountingEmbedder embedder = new();
        using WebApplicationFactory<Program> factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("Rag:MaxChunkCount", "2");
                builder.ConfigureServices(services =>
                {
                    TestSupport.RemoveAll(services, typeof(IEmbedder));
                    TestSupport.RemoveAll(services, typeof(IChromaService));
                    services.AddSingleton<IEmbedder>(embedder);
                    services.AddSingleton<IChromaService>(new TestSupport.StubChroma());
                });
            });
        HttpClient client = factory.CreateClient();

        HttpResponseMessage created = await client.PostAsync("/session", content: null);
        TestSupport.SessionResponse? session = await created.Content.ReadFromJsonAsync<TestSupport.SessionResponse>();
        Assert.NotNull(session);

        using MultipartFormDataContent form = new();
        for (int i = 0; i < 3; i++)
        {
            form.Add(
                new ByteArrayContent(Encoding.UTF8.GetBytes($"# note {i}\nshort body")),
                "files",
                $"note{i}.md");
        }

        HttpResponseMessage response = await client.PostAsync($"/session/{session!.SessionId}/md", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        Assert.Contains("chunks", body);
        Assert.Equal(0, embedder.RangeCalls);
        Assert.Equal(0, embedder.EmbedCalls);
    }

    [Fact]
    public async Task Upload_at_exactly_the_cap_is_accepted()
    {
        using WebApplicationFactory<Program> factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("Rag:MaxChunkCount", "2");
                builder.ConfigureServices(services =>
                {
                    TestSupport.RemoveAll(services, typeof(IEmbedder));
                    TestSupport.RemoveAll(services, typeof(IChromaService));
                    services.AddSingleton<IEmbedder>(new CountingEmbedder());
                    services.AddSingleton<IChromaService>(new TestSupport.StubChroma());
                });
            });
        HttpClient client = factory.CreateClient();

        HttpResponseMessage created = await client.PostAsync("/session", content: null);
        TestSupport.SessionResponse? session = await created.Content.ReadFromJsonAsync<TestSupport.SessionResponse>();
        Assert.NotNull(session);

        using MultipartFormDataContent form = new();
        for (int i = 0; i < 2; i++)
        {
            form.Add(
                new ByteArrayContent(Encoding.UTF8.GetBytes($"# note {i}\nshort body")),
                "files",
                $"note{i}.md");
        }

        HttpResponseMessage response = await client.PostAsync($"/session/{session!.SessionId}/md", form);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"stored\":2", body);
    }

    private sealed class CountingEmbedder : IEmbedder
    {
        public int EmbedCalls;
        public int RangeCalls;

        public float[] Embed(string text)
        {
            EmbedCalls++;
            return [1f, 0f];
        }

        public IEnumerable<DocumentChunk> EmbeddRange(IEnumerable<DocumentChunk> documents)
        {
            RangeCalls++;
            return documents;
        }
    }
}
