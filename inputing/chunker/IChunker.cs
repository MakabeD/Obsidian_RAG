namespace chunker
{
    public interface IChunker
    {
        static abstract IEnumerable<string> Chunking(string text, int characterThreshold);
    }
}
