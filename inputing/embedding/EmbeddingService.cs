using chunker;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.ML.Tokenizers;


public class EmbeddingService
{
    private readonly InferenceSession _session;
    private readonly WordPieceTokenizer _tokenizer;

    public EmbeddingService(string modelPath, string vocabPath)
    {
        using var vocabStream = File.OpenRead(vocabPath);
        
        _tokenizer = WordPieceTokenizer.Create(vocabStream);
        _session = new InferenceSession(modelPath);
    }
    public float[] Embed(string text)
    {
        IReadOnlyList<int>rawTokenIds=_tokenizer.EncodeToIds(text);

        List<long> idsList=new List<long>(rawTokenIds.Count + 2) { 101 };
        idsList.AddRange(rawTokenIds.Select(r=>(long)r));
        idsList.Add(102);

        long[] idsArray= idsList.Select(r => (long)r).ToArray();
        long[] attentionMask= Enumerable.Repeat(1L, idsArray.Length).ToArray();
        int batchsize=1;
        int sequenceLength=idsArray.Length;
        var inputIdsTensor= new DenseTensor<long>(idsArray, new[] {batchsize, sequenceLength});
        var attentionMaskTensor = new DenseTensor<long>(attentionMask,new[] {batchsize, sequenceLength});
        var tokenTypesIdsTensor = new DenseTensor<long>(new long[sequenceLength], new[] {batchsize, sequenceLength});

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input_ids", inputIdsTensor),
            NamedOnnxValue.CreateFromTensor("attention_mask", attentionMaskTensor),
            NamedOnnxValue.CreateFromTensor("token_type_ids", tokenTypesIdsTensor),

        };
        using var result= _session.Run(inputs);
        var outputTensor= result.First().AsTensor<float>();
        var hiddenSize=outputTensor.Dimensions[2];
        // mean pooling 
        float[] embedding= new float[hiddenSize];
        for (int i=0; i<sequenceLength; i++)
        {
            for(int j=0;j<hiddenSize;j++)
            {
                embedding[j]+=outputTensor[0, i, j];
            }
        }
        for (int i=0; i<hiddenSize;i++)
        {
            embedding[i]/=sequenceLength;
        }
        
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