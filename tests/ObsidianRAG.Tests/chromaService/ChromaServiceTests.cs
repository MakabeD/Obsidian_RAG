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

    private sealed class CountingChromaService(IOptions<RagOptions> options)
        : ChromaService(new HttpClient(), options, NullLogger<ChromaService>.Instance)
    {
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