using chunker;
using configuration;
using Microsoft.Extensions.Options;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.ML.Tokenizers;

public class EmbeddingService : IDisposable
{
    private readonly InferenceSession _session;
    private readonly WordPieceTokenizer _tokenizer;
    private readonly int _maxTokenLength;
    private readonly ILogger<EmbeddingService> _logger;

    public EmbeddingService(IOptions<RagOptions> options, ILogger<EmbeddingService> logger)
    {
        RagOptions opts = options.Value;
        _logger = logger;
        _maxTokenLength = opts.MaxTokenLength;

        using var vocabStream = File.OpenRead(opts.VocabPath);
        _tokenizer = WordPieceTokenizer.Create(vocabStream);
        _session = new InferenceSession(opts.ModelPath);
    }

    public float[] Embed(string text)
    {
        IReadOnlyList<int> rawTokenIds = _tokenizer.EncodeToIds(text);

        int maxContent = Math.Max(1, _maxTokenLength - 2);
        if (rawTokenIds.Count > maxContent)
        {
            _logger.LogWarning("Truncating text from {Original} to {Max} tokens for embedding", rawTokenIds.Count, _maxTokenLength);
            rawTokenIds = rawTokenIds.Take(maxContent).ToList();
        }

        long[] idsArray = new long[rawTokenIds.Count + 2];
        idsArray[0] = 101;
        for (int i = 0; i < rawTokenIds.Count; i++) idsArray[i + 1] = rawTokenIds[i];
        idsArray[^1] = 102;

        int sequenceLength = idsArray.Length;
        long[] attentionMask = Enumerable.Repeat(1L, sequenceLength).ToArray();
        int batchSize = 1;

        var inputIdsTensor = new DenseTensor<long>(idsArray, new[] { batchSize, sequenceLength });
        var attentionMaskTensor = new DenseTensor<long>(attentionMask, new[] { batchSize, sequenceLength });
        var tokenTypesIdsTensor = new DenseTensor<long>(new long[sequenceLength], new[] { batchSize, sequenceLength });

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input_ids", inputIdsTensor),
            NamedOnnxValue.CreateFromTensor("attention_mask", attentionMaskTensor),
            NamedOnnxValue.CreateFromTensor("token_type_ids", tokenTypesIdsTensor),
        };

        using var result = _session.Run(inputs);
        var outputTensor = result.First().AsTensor<float>();
        int hiddenSize = outputTensor.Dimensions[2];

        float[] embedding = new float[hiddenSize];
        for (int i = 0; i < sequenceLength; i++)
        {
            for (int j = 0; j < hiddenSize; j++)
            {
                embedding[j] += outputTensor[0, i, j];
            }
        }
        for (int j = 0; j < hiddenSize; j++) embedding[j] /= sequenceLength;

        return embedding;
    }

    public IEnumerable<DocumentChunk> EmbeddRange(IEnumerable<DocumentChunk> documents)
    {
        foreach (var doc in documents)
        {
            doc.Embedding = Embed(doc.Content);
            yield return doc;
        }
    }

    public void Dispose()
    {
        _session?.Dispose();
    }
}
