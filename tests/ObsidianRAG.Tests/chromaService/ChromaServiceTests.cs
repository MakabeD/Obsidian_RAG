using System.Net;
using System.Text.Json;
using configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace ObsidianRAG.Tests.chromaService;

public class ChromaServiceTests
{
    [Fact]
    public async Task InitializeAsync_resolves_the_collection_id_only_once()
    {
        CountingChromaService service = new(Options.Create(new RagOptions()));

        await service.InitializeAsync();
        await service.InitializeAsync();
        await service.InitializeAsync();

        Assert.Equal(1, service.ResolveCalls);
    }

    [Fact]
    public async Task InitializeAsync_does_not_cache_a_failed_resolution()
    {
        FlakyChromaService service = new(Options.Create(new RagOptions()));

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.InitializeAsync());
        await service.InitializeAsync();
        await service.InitializeAsync();

        Assert.Equal(2, service.ResolveCalls);
    }

    [Fact]
    public async Task AddSessionRecordsAsync_splits_documents_into_batches()
    {
        PostRecordingHandler handler = new();
        TestChromaService service = new(
            new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:65535") },
            Options.Create(new RagOptions { ChromaAddBatchSize = 2 }));

        await service.InitializeAsync();

        List<ChromaDocument> documents = Enumerable.Range(0, 5)
            .Select(i => new ChromaDocument(
                $"id{i}",
                [1f],
                $"content{i}",
                new Dictionary<string, object>()))
            .ToList();

        await service.AddSessionRecordsAsync("session", documents);

        Assert.Equal([2, 2, 1], handler.BatchSizes);
    }

    private sealed class TestChromaService(HttpClient client, IOptions<RagOptions> options)
        : ChromaService(client, options, NullLogger<ChromaService>.Instance)
    {
        protected override Task<string?> ResolveCollectionIdAsync(CancellationToken ct)
            => Task.FromResult("stub-collection-id");
    }

    private sealed class PostRecordingHandler : HttpMessageHandler
    {
        public List<int> BatchSizes { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (request.Method == HttpMethod.Post)
            {
                string body = await request.Content!.ReadAsStringAsync(ct);
                using JsonDocument json = JsonDocument.Parse(body);
                BatchSizes.Add(json.RootElement.GetProperty("ids").GetArrayLength());
            }

            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    private sealed class CountingChromaService(IOptions<RagOptions> options)
        : ChromaService(new HttpClient(), options, NullLogger<ChromaService>.Instance)    {
        public int ResolveCalls;

        protected override Task<string?> ResolveCollectionIdAsync(CancellationToken ct)
        {
            ResolveCalls++;
            return Task.FromResult("stub-collection-id");
        }
    }

    private sealed class FlakyChromaService(IOptions<RagOptions> options)
        : ChromaService(new HttpClient(), options, NullLogger<ChromaService>.Instance)
    {
        public int ResolveCalls;
        private bool _failed;

        protected override Task<string?> ResolveCollectionIdAsync(CancellationToken ct)
        {
            ResolveCalls++;
            if (!_failed)
            {
                _failed = true;
                throw new InvalidOperationException("upstream down");
            }

            return Task.FromResult("stub-collection-id");
        }
    }
}