using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using chunker;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ObsidianRAG.Tests.program;

[Collection(nameof(WebHost))]
public class ZipUploadTests
{
    [Fact]
    public async Task Upload_with_two_cap_passing_zips_is_rejected_before_any_embedding()
    {
        CountingEmbedder embedder = new();
        CountingChroma chroma = new();

        using WebApplicationFactory<Program> factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("Rag:MaxZipTotalUncompressedBytes", "100");
                builder.ConfigureServices(services =>
                {
                    TestSupport.RemoveAll(services, typeof(IEmbedder));
                    TestSupport.RemoveAll(services, typeof(IChromaService));
                    services.AddSingleton<IEmbedder>(embedder);
                    services.AddSingleton<IChromaService>(chroma);
                });
            });
        HttpClient client = factory.CreateClient();

        HttpResponseMessage created = await client.PostAsync("/session", content: null);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        TestSupport.SessionResponse? session = await created.Content.ReadFromJsonAsync<TestSupport.SessionResponse>();
        Assert.NotNull(session);

        using MultipartFormDataContent form = new();
        form.Add(
            new ByteArrayContent(MakeZip(("a.md", new string('a', 60)))),
            "files",
            "a.zip");
        form.Add(
            new ByteArrayContent(MakeZip(("b.md", new string('b', 60)))),
            "files",
            "b.zip");

        HttpResponseMessage response = await client.PostAsync(
            $"/session/{session!.SessionId}/md",
            form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Unsafe or oversized zip archive", body);
        Assert.Equal(0, embedder.RangeCalls);
        Assert.Equal(0, chroma.AddCalls);
    }

    private static byte[] MakeZip(params (string Name, string Content)[] entries)
    {
        using MemoryStream stream = new();
        using (ZipArchive zip = new(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach ((string name, string content) in entries)
            {
                ZipArchiveEntry entry = zip.CreateEntry(name);
                using StreamWriter writer = new(entry.Open(), Encoding.UTF8);
                writer.Write(content);
            }
        }

        return stream.ToArray();
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

    private sealed class CountingChroma : IChromaService
    {
        public int InitializeCalls;
        public int AddCalls;

        public Task InitializeAsync(CancellationToken ct = default)
        {
            InitializeCalls++;
            return Task.CompletedTask;
        }

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
