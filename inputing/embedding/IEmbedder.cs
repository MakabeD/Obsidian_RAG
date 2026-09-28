using chunker;

public interface IEmbedder
{
    float[] Embed(string text, CancellationToken ct = default);

    IEnumerable<DocumentChunk> EmbeddRange(IEnumerable<DocumentChunk> documents, CancellationToken ct = default);
}