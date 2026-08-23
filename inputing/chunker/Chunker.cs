using vaultReader;
namespace chunker
{
    
    public class Chunker:IChunker
    {
        public static IEnumerable<DocumentChunk> Chunking(DocumentData document, int characterThreshold)
        {
            string text = document.Content;
            int chunkIndex = 1;

            int dotsearch(string chunk, int middle_pos)
            {
                for (int j = 1; j < middle_pos; j++)
                {
                    if (chunk[middle_pos + j] == '.') return middle_pos + j;
                    if (chunk[middle_pos - j] == '.') return middle_pos + j;
                }
                return middle_pos;
            }

            string chunk = "#";
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] != '#')
                {
                    chunk += text[i];
                }
                else if (chunk.Length > 1) 
                {
                    if (chunk.Length > characterThreshold && (chunk.Length / characterThreshold) >= 1.2)
                    {
                        int dotpos = dotsearch(chunk, (int)(chunk.Length / characterThreshold));
                        
                        
                        yield return new DocumentChunk { 
                            Id = $"{document.FileName}_{chunkIndex++:D3}",
                            Content = chunk.Substring(0, dotpos + 1), 
                            Metadata = new Metadata { Source = document.Source, FileName = document.FileName } 
                        };
                        yield return new DocumentChunk { 
                            Id = $"{document.FileName}_{chunkIndex++:D3}",
                            Content = chunk.Substring(dotpos - 1), 
                            Metadata = new Metadata { Source = document.Source, FileName = document.FileName } 
                        };
                    }
                    else
                    {
                        yield return new DocumentChunk { 
                            Id = $"{document.FileName}_{chunkIndex++:D3}",
                            Content = chunk, 
                            Metadata = new Metadata { Source = document.Source, FileName = document.FileName } 
                        };
                    }
                    
                    chunk = "#";
                }
            }
            
            if (chunk.Length > 1)
            {
                yield return new DocumentChunk { 
                    Id = $"{document.FileName}_{chunkIndex++:D3}",
                    Content = chunk, 
                    Metadata = new Metadata { Source = document.Source, FileName = document.FileName } 
                };
            }
        }
    }
}