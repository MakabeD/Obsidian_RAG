namespace chunker
{
    public class DocumentChunk()
    {
        public required string Id {get;set;}
        public required string Content {get;set;}
        public required float[] Embedding {get;set;}
        public Dictionary<string, string>? Metadata {get;set;}
    }
}