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
    public void Embed(string text)
    {
        IReadOnlyList<int>tokenids=_tokenizer.EncodeToIds(text);
        long[] idsArray= tokenids.Select(r => (long)r).ToArray();
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
        var outputTensor= _session.Run(inputs);
        Console.WriteLine(outputTensor);
        // TODO: terminar el metodo= mean pooling + generar embeding + rectificar el return de la funcion en vez de void
    }
    
}