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

    [Fact]
    public async Task Parallel_InitializeAsync_calls_resolve_the_collection_only_once()
    {
        GatedCountingChromaService service = new(Options.Create(new RagOptions()));

        Task[] tasks = Enumerable.Range(0, 5)
            .Select(_ => Task.Run(() => service.InitializeAsync()))
            .ToArray();

        await Task.WhenAny(service.AllArrived.Task, Task.Delay(TimeSpan.FromSeconds(1)));
        service.Release.TrySetResult();

        await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(1, service.ResolveEntries);
        Assert.All(tasks, t => Assert.Equal(TaskStatus.RanToCompletion, t.Status));
    }

    [Fact]
    public async Task A_null_resolved_id_fails_InitializeAsync_with_a_clear_upstream_error()
    {
        NullResolvingChromaService service = new(Options.Create(new RagOptions()));

        HttpRequestException exception = await Assert.ThrowsAsync<HttpRequestException>(
            () => service.InitializeAsync());

        Assert.Contains("id", exception.Message);
    }

    [Fact]
    public async Task A_malformed_collection_body_fails_InitializeAsync_with_a_clear_upstream_error()
    {
        ChromaService service = new(
            new HttpClient(new MalformedBodyHandler()) { BaseAddress = new Uri("http://127.0.0.1:65535") },
            Options.Create(new RagOptions()),
            NullLogger<ChromaService>.Instance);

        HttpRequestException exception = await Assert.ThrowsAsync<HttpRequestException>(
            () => service.InitializeAsync());

        Assert.Contains("id", exception.Message);
    }

    private sealed class MalformedBodyHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{ "name": "vault_collection" }""", System.Text.Encoding.UTF8, "application/json")
            });
    }

    private sealed class TestChromaService(HttpClient client, IOptions<RagOptions> options)
        : ChromaService(client, options, NullLogger<ChromaService>.Instance)
    {
        protected override Task<string?> ResolveCollectionIdAsync(CancellationToken ct)
            => Task.FromResult("stub-collection-id");
    }

    private sealed class GatedCountingChromaService(IOptions<RagOptions> options)
        : ChromaService(new HttpClient(), options, NullLogger<ChromaService>.Instance)
    {
        public int ResolveEntries;

        public TaskCompletionSource AllArrived { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Release { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override async Task<string?> ResolveCollectionIdAsync(CancellationToken ct)
        {
            Interlocked.Increment(ref ResolveEntries);
            if (ResolveEntries == 5)
            {
                AllArrived.TrySetResult();
            }

            await Release.Task;
            return "stub-collection-id";
        }
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

    private sealed class NullResolvingChromaService(IOptions<RagOptions> options)
        : ChromaService(new HttpClient(), options, NullLogger<ChromaService>.Instance)
    {
        protected override Task<string?> ResolveCollectionIdAsync(CancellationToken ct)
            => Task.FromResult<string?>(null);
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