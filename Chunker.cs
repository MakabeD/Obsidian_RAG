namespace obsidian_RAG
{
    
    public class Chunk:IChunk
    {
        public static IEnumerable<string> Chunker(string text)
        {
            // TODO: Escribir un chunker simple por parrafos, y si hay \n\t entonces no splitear 
            yield return "";
        }
    }
}
