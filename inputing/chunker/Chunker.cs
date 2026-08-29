using System.Text;
using vaultReader;
namespace chunker
{
    public class Chunker:IChunker
    {
        public static IEnumerable<DocumentChunk> Chunking(DocumentData document, int characterThreshold)
        {
            int chunkIndex = 1;
            string[] lines = document.Content.Split('\n');
            var chunk = new StringBuilder();
            bool inCodeFence = false;

            IEnumerable<DocumentChunk> Emit(string content)
            {
                float ratio = (float)content.Length / characterThreshold;
                IEnumerable<string> pieces = ratio >= 1.2f
                    ? SplitIntoPieces(content, characterThreshold)
                    : new[] { content };

                foreach (string piece in pieces)
                {
                    yield return new DocumentChunk
                    {
                        Id = $"{document.FileName}_{chunkIndex++:D3}",
                        Content = piece,
                        Metadata = new Metadata { Source = document.Source, FileName = document.FileName }
                    };
                }
            }

            for (int i = 0; i < lines.Length; i++)
            {
                string rawLine = lines[i];
                string line = rawLine.TrimEnd('\r');

                bool isFence = line.TrimStart().StartsWith("```");
                bool isHeading = !inCodeFence && !isFence && line.StartsWith('#');

                if (isHeading)
                {
                    if (chunk.Length > 0)
                    {
                        foreach (DocumentChunk dc in Emit(chunk.ToString())) yield return dc;
                        chunk.Clear();
                    }
                    chunk.Append('#').Append(line.TrimStart('#'));
                }
                else
                {
                    chunk.Append(rawLine);
                }

                if (i < lines.Length - 1) chunk.Append('\n');
                if (isFence) inCodeFence = !inCodeFence;
            }

            if (chunk.Length > 0)
            {
                foreach (DocumentChunk dc in Emit(chunk.ToString())) yield return dc;
            }
        }

        public static IEnumerable<DocumentChunk> Chunking(IEnumerable<DocumentData> documents, int characterThreshold)
        {
            foreach (DocumentData document in documents)
            {
                foreach (DocumentChunk chunk in Chunking(document, characterThreshold))
                {
                    yield return chunk;
                }
            }
        }

        private static IEnumerable<string> SplitIntoPieces(string text, int threshold)
        {
            int start = 0;
            while (text.Length - start >= threshold)
            {
                int center = Math.Min(start + threshold, text.Length - 1);
                int cut = FindNearestDot(
                    text,
                    center,
                    Math.Max(center - threshold / 2, start),
                    Math.Min(center + threshold / 2, text.Length - 1));

                if (cut < 0) cut = center;

                yield return text.Substring(start, cut - start + 1);
                start = cut + 1;
            }

            if (start < text.Length) yield return text.Substring(start);
        }

        private static int FindNearestDot(string text, int middlePos, int minBound, int maxBound)
        {
            if (text[middlePos] == '.') return middlePos;

            int maxRadius = Math.Min(middlePos - minBound, maxBound - middlePos);
            for (int j = 1; j <= maxRadius; j++)
            {
                if (text[middlePos + j] == '.') return middlePos + j;
                if (text[middlePos - j] == '.') return middlePos - j;
            }
            return -1;
        }
    }
}
