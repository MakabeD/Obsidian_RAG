using vaultReader;
namespace chunker
{
    public interface IChunker
    {
        static abstract IEnumerable<DocumentChunk> Chunking(DocumentData document, int characterThreshold);

        static abstract IEnumerable<DocumentChunk> Chunking(IEnumerable<DocumentData> documents, int characterThreshold);
    }
}
