namespace obsidian_RAG
{
    
    public class Chunk:IChunk
    {
        public static IEnumerable<string> Chunker(string text, int characterThreshold)
        {
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
            for (int i =0;i<text.Length;i++)
            {
                if (text[i]!='#')
                {
                    chunk += text[i];

                }
                else if(chunk.Length>1) 
                {
                    if (chunk.Length>characterThreshold&&(chunk.Length/characterThreshold) >= 1.2 )
                    {
                        int dotpos = dotsearch(chunk,(int)(chunk.Length / characterThreshold));
                        // substring para evitar mas bucles no optimizados
                        yield return chunk.Substring(0,dotpos+1);
                        yield return chunk.Substring(dotpos-1);
                    }
                    yield return chunk; 
                    chunk= "#";
                }
            }
            yield return chunk;
        }
    }
}
