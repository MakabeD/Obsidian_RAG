namespace chunker
{
    public class DocumentChunk()
    {
        public required string Id {get;set;}
        public required string Content {get;set;}
        public float[]? Embedding {get;set;}
        public Metadata? Metadata {get;set;}
    }
}