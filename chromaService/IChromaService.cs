public interface IChromaService
{
    Task InitializeAsync(CancellationToken ct = default);

    Task AddSessionRecordsAsync(string sessionId, List<ChromaDocument> documents, CancellationToken ct = default);

    Task<List<SearchResult>> QuerySessionAsync(string sessionId, float[] queryEmbedding, int topK, CancellationToken ct = default);

    Task TerminateSessionAsync(string sessionId, CancellationToken ct = default);
}
