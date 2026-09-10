using System.Security.Cryptography;
using System.Text;

namespace chunker;

public static class ChunkIdRewriter
{
    public static List<DocumentChunk> RewriteChunkIds(List<DocumentChunk> chunks, string sessionId)
    {
        string prefix = $"{sessionId}_";
        for (int i = 0; i < chunks.Count; i++)
        {
            DocumentChunk c = chunks[i];
            string contentHash = ShortHash(c.Content);
            string safeName = SanitizeForId(c.Metadata?.FileName ?? "chunk");
            c.Id = $"{prefix}{safeName}_{contentHash}_{i:D4}";
        }
        return chunks;
    }

    public static string SanitizeForId(string s)
    {
        StringBuilder sb = new(s.Length);
        foreach (char ch in s)
        {
            sb.Append(char.IsLetterOrDigit(ch) || ch is '-' or '_' or '.' ? ch : '_');
        }
        return sb.ToString();
    }

    public static string ShortHash(string content)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(hash, 0, 4).ToLowerInvariant();
    }
}
