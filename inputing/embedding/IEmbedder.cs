using chunker;

public interface IEmbedder
{
    float[] Embed(string text);

    IEnumerable<DocumentChunk> EmbeddRange(IEnumerable<DocumentChunk> documents);
}
