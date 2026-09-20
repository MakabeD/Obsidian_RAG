using chunker;
using configuration;
using Microsoft.Extensions.Options;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.ML.Tokenizers;

public class EmbeddingService : IEmbedder, IDisposable
{
    private readonly InferenceSession _session;
    private readonly WordPieceTokenizer _tokenizer;
    private readonly int _maxTokenLength;
    private readonly int _batchSize;
    private readonly ILogger<EmbeddingService> _logger;
    private readonly RagOptions _opts;

    public EmbeddingService(IOptions<RagOptions> options, ILogger<EmbeddingService> logger)
    {
        _opts = options.Value;
        _logger = logger;
        _maxTokenLength = _opts.MaxTokenLength;
        _batchSize = Math.Max(1, _opts.EmbedBatchSize);

        using var vocabStream = File.OpenRead(_opts.VocabPath);
        _tokenizer = WordPieceTokenizer.Create(vocabStream);
        _session = new InferenceSession(_opts.ModelPath);
    }

    public bool IsLoaded =>
        _session is not null
        && _tokenizer is not null
        && File.Exists(_opts.ModelPath)
        && File.Exists(_opts.VocabPath);

    public float[] Embed(string text)
    {
        long[] ids = TokenizeToWrappedIds(text);

        var inputs = BuildSessionInputs([ids]);

        using var result = _session.Run(inputs);
        var outputTensor = result.First().AsTensor<float>();

        return EmbeddingBatching.MeanPool(outputTensor, [ids.Length]).Single();
    }

    public IEnumerable<DocumentChunk> EmbeddRange(IEnumerable<DocumentChunk> documents)
    {
        List<(DocumentChunk Chunk, long[] Ids)> buffer = new(_batchSize);
        foreach (DocumentChunk doc in documents)
        {
            buffer.Add((doc, TokenizeToWrappedIds(doc.Content)));
            if (buffer.Count == _batchSize)
            {
                foreach (DocumentChunk chunk in EmbedBatch(buffer))
                {
                    yield return chunk;
                }

                buffer.Clear();
            }
        }

        if (buffer.Count > 0)
        {
            foreach (DocumentChunk chunk in EmbedBatch(buffer))
            {
                yield return chunk;
            }
        }
    }

    private IEnumerable<DocumentChunk> EmbedBatch(List<(DocumentChunk Chunk, long[] Ids)> batch)
    {
        var inputs = BuildSessionInputs(batch.Select(x => x.Ids).ToArray());

        using var result = _session.Run(inputs);
        var outputTensor = result.First().AsTensor<float>();

        float[][] pooled = EmbeddingBatching.MeanPool(
            outputTensor,
            batch.Select(x => x.Ids.Length).ToArray());

        for (int i = 0; i < batch.Count; i++)
        {
            batch[i].Chunk.Embedding = pooled[i];
        }

        return batch.Select(x => x.Chunk);
    }

    private List<NamedOnnxValue> BuildSessionInputs(long[][] ids)
    {
        (DenseTensor<long> inputIds, DenseTensor<long> attentionMask, DenseTensor<long> tokenTypeIds) =
            EmbeddingBatching.BuildInputs(ids);

        return
        [
            NamedOnnxValue.CreateFromTensor("input_ids", inputIds),
            NamedOnnxValue.CreateFromTensor("attention_mask", attentionMask),
            NamedOnnxValue.CreateFromTensor("token_type_ids", tokenTypeIds),
        ];
    }

    private long[] TokenizeToWrappedIds(string text)
    {
        IReadOnlyList<int> rawTokenIds = _tokenizer.EncodeToIds(text);

        if (rawTokenIds.Count > Math.Max(1, _maxTokenLength - 2))
        {
            _logger.LogWarning(
                "Truncating text from {Original} to {Max} tokens for embedding",
                rawTokenIds.Count, _maxTokenLength);
        }

        return EmbeddingBatching.WrapAndTruncate(rawTokenIds, _maxTokenLength);
    }

    public void Dispose()
    {
        _session?.Dispose();
    }
}
