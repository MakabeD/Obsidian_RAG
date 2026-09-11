using chunker;
using vaultReader;
using Xunit;

namespace ObsidianRAG.Tests.inputing;

public class ChunkerTests
{
    private const int Threshold = 600;

    [Fact]
    public void Heading_levels_are_preserved_verbatim_in_chunk_content()
    {
        DocumentData document = Doc("# Top\nbody\n### Deep\nmore");

        List<DocumentChunk> chunks = Chunker.Chunking(document, Threshold).ToList();

        Assert.Contains(chunks, c => c.Content.Contains("### Deep"));
        Assert.All(chunks, c => Assert.DoesNotContain("#Deep", c.Content));
    }

    [Fact]
    public void A_heading_still_starts_a_new_chunk_and_leads_it()
    {
        DocumentData document = Doc("intro\n## Second\nmore");

        List<DocumentChunk> chunks = Chunker.Chunking(document, Threshold).ToList();

        Assert.Equal(2, chunks.Count);
        Assert.StartsWith("intro", chunks[0].Content);
        Assert.StartsWith("## Second", chunks[1].Content);
    }

    private static DocumentData Doc(string content) => new()
    {
        Source = "zip",
        FileName = "note.md",
        Content = content
    };
}
