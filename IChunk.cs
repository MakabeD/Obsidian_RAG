namespace obsidian_RAG
{
    public interface IChunk
    {
        static abstract IEnumerable<string> Chunker(string text, int characterThreshold);
    }
}
