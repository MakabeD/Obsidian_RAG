using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ObsidianRAG.Tests.program;

[CollectionDefinition(nameof(WebHost))]
public sealed class WebHost;

internal static class TestSupport
{
    internal static void RemoveAll(IServiceCollection services, Type t)
    {
        for (int i = services.Count - 1; i >= 0; i--)
        {
            if (services[i].ServiceType == t)
                services.RemoveAt(i);
        }
    }

    internal sealed record SessionResponse(string SessionId);

    internal sealed class StubChroma : IChromaService
    {
        public Task InitializeAsync(CancellationToken ct = default) => Task.CompletedTask;

        public Task AddSessionRecordsAsync(string sessionId, List<ChromaDocument> documents, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task<List<SearchResult>> QuerySessionAsync(string sessionId, float[] queryEmbedding, int topK, CancellationToken ct = default)
            => Task.FromResult(new List<SearchResult>());

        public Task TerminateSessionAsync(string sessionId, CancellationToken ct = default) => Task.CompletedTask;
    }
}
