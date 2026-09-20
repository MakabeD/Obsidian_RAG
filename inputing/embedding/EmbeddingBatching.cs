using Microsoft.ML.OnnxRuntime.Tensors;

public static class EmbeddingBatching
{
    public static long[] WrapAndTruncate(IReadOnlyList<int> rawIds, int maxTokenLength)
    {
        int maxContent = Math.Max(1, maxTokenLength - 2);
        int contentLength = Math.Min(rawIds.Count, maxContent);

        long[] ids = new long[contentLength + 2];
        ids[0] = 101;
        for (int i = 0; i < contentLength; i++)
        {
            ids[i + 1] = rawIds[i];
        }

        ids[^1] = 102;
        return ids;
    }

    public static List<long[][]> Partition(long[][] ids, int batchSize)
    {
        List<long[][]> batches = new();
        for (int start = 0; start < ids.Length; start += batchSize)
        {
            int count = Math.Min(batchSize, ids.Length - start);
            long[][] batch = new long[count][];
            Array.Copy(ids, start, batch, 0, count);
            batches.Add(batch);
        }

        return batches;
    }

    public static (DenseTensor<long> InputIds, DenseTensor<long> AttentionMask, DenseTensor<long> TokenTypeIds)
        BuildInputs(long[][] ids)
    {
        int batchSize = ids.Length;
        int sequenceLength = ids.Max(row => row.Length);

        DenseTensor<long> inputIds = new(new[] { batchSize, sequenceLength });
        DenseTensor<long> attentionMask = new(new[] { batchSize, sequenceLength });
        DenseTensor<long> tokenTypeIds = new(new[] { batchSize, sequenceLength });

        for (int b = 0; b < batchSize; b++)
        {
            for (int i = 0; i < sequenceLength; i++)
            {
                bool real = i < ids[b].Length;
                inputIds[b, i] = real ? ids[b][i] : 0;
                attentionMask[b, i] = real ? 1 : 0;
                tokenTypeIds[b, i] = 0;
            }
        }

        return (inputIds, attentionMask, tokenTypeIds);
    }

    public static float[][] MeanPool(Tensor<float> output, int[] lengths)
    {
        int batchSize = output.Dimensions[0];
        int hiddenSize = output.Dimensions[2];

        float[][] pooled = new float[batchSize][];
        for (int b = 0; b < batchSize; b++)
        {
            float[] embedding = new float[hiddenSize];
            for (int i = 0; i < lengths[b]; i++)
            {
                for (int j = 0; j < hiddenSize; j++)
                {
                    embedding[j] += output[b, i, j];
                }
            }

            for (int j = 0; j < hiddenSize; j++)
            {
                embedding[j] /= lengths[b];
            }

            pooled[b] = embedding;
        }

        return pooled;
    }
}
