using chunker;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using configuration;
using Xunit;

namespace ObsidianRAG.Tests.inputing;

public class EmbeddingServiceTests
{
    [Fact]
    public void Range_embeddings_match_the_per_chunk_embedding_of_the_same_text()
    {
        string[] contents =
        [
            "apple banana cherry",
            string.Join(' ', Enumerable.Repeat("word", 150)),
            string.Join(' ', Enumerable.Range(0, 100).Select(i => $"note{i} heading fragment")),
            "ping " + new string('a', 3800),
        ];

        string modelDir = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "model"));

        using EmbeddingService service = new(
            Options.Create(new RagOptions
            {
                ModelPath = Path.Combine(modelDir, "model.onnx"),
                VocabPath = Path.Combine(modelDir, "vocab.txt")
            }),
            NullLogger<EmbeddingService>.Instance);

        List<DocumentChunk> chunks = contents
            .Select((c, i) => new DocumentChunk { Id = $"c{i}", Content = c })
            .ToList();

        service.EmbeddRange(chunks).ToList();

        Assert.All(chunks, chunk => Assert.NotNull(chunk.Embedding));

        for (int i = 0; i < contents.Length; i++)
        {
            float[] single = service.Embed(contents[i]);
            float tolerance = single.Max(Math.Abs) * 1e-3f;
            Assert.True(
                chunks[i].Embedding!.Zip(single, (a, b) => Math.Abs(a - b)).All(d => d <= tolerance),
                $"embedding of chunk '{chunks[i].Id}' diverged from its single-row embedding");
        }
    }
}
