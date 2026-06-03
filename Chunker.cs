namespace obsidian_RAG
{
    
    public class Chunk:IChunk
    {
        public static IEnumerable<string> Chunker(string text)
        {
            Console.WriteLine(text);
            yield return text;

            yield return text.ToLower();
        }
    }
}
