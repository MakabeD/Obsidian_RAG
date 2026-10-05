using Microsoft.ML.OnnxRuntime.Tensors;
using Xunit;

namespace ObsidianRAG.Tests.inputing;

public class EmbeddingBatchingTests
{
    [Fact]
    public void Short_ids_are_wrapped_with_cls_and_sep()
    {
        long[] wrapped = EmbeddingBatching.WrapAndTruncate([5, 6, 7], maxTokenLength: 512);

        Assert.Equal([101, 5, 6, 7, 102], wrapped);
    }

    [Fact]
    public void Empty_ids_collapse_to_cls_and_sep_only()
    {
        long[] wrapped = EmbeddingBatching.WrapAndTruncate([], maxTokenLength: 512);

        Assert.Equal([101, 102], wrapped);
    }

    [Fact]
    public void Ids_exactly_at_maxContent_are_not_truncated()
    {
        int[] raw = Enumerable.Range(0, 510).ToArray();

        long[] wrapped = EmbeddingBatching.WrapAndTruncate(raw, maxTokenLength: 512);

        Assert.Equal(512, wrapped.Length);
        Assert.Equal(101, wrapped[0]);
        Assert.Equal(102, wrapped[^1]);
        Assert.Equal(0, wrapped[1]);
        Assert.Equal(509, wrapped[^2]);
    }

    [Fact]
    public void Ids_over_maxContent_are_truncated_from_the_end()
    {
        int[] raw = Enumerable.Range(0, 600).ToArray();

        long[] wrapped = EmbeddingBatching.WrapAndTruncate(raw, maxTokenLength: 512);

        Assert.Equal(512, wrapped.Length);
        Assert.Equal(101, wrapped[0]);
        Assert.Equal(102, wrapped[^1]);
        Assert.Equal(509, wrapped[^2]);
        Assert.Equal(0, wrapped[1]);
    }

    [Fact]
    public void Partition_splits_rows_into_batches_of_the_requested_size()
    {
        long[][] ids = [[1], [2], [3], [4], [5]];

        List<long[][]> batches = EmbeddingBatching.Partition(ids, batchSize: 2);

        Assert.Equal(3, batches.Count);
        Assert.Equal(new[] { 1L }, batches[0][0]);
        Assert.Equal(new[] { 2L }, batches[0][1]);
        Assert.Equal(new[] { 3L }, batches[1][0]);
        Assert.Equal(new[] { 4L }, batches[1][1]);
        Assert.Equal(new[] { 5L }, batches[2][0]);
    }

    [Fact]
    public void Partition_with_an_exact_multiple_makes_no_partial_batch()
    {
        long[][] ids = [[1], [2], [3], [4]];

        List<long[][]> batches = EmbeddingBatching.Partition(ids, batchSize: 2);

        Assert.Equal(2, batches.Count);
        Assert.All(batches, b => Assert.Equal(2, b.Length));
    }

    [Fact]
    public void BuildInputs_pads_rows_to_the_batch_max_and_masks_the_padding()
    {
        long[][] ids =
        [
            [101, 5, 102],
            [101, 6, 7, 102],
        ];

        (DenseTensor<long> inputIds, DenseTensor<long> attentionMask, DenseTensor<long> tokenTypeIds) =
            EmbeddingBatching.BuildInputs(ids);

        Assert.Equal(new[] { 2, 4 }, inputIds.Dimensions.ToArray());
        Assert.Equal([101, 5, 102, 0], Row(inputIds, 0));
        Assert.Equal([101, 6, 7, 102], Row(inputIds, 1));
        Assert.Equal([1, 1, 1, 0], Row(attentionMask, 0));
        Assert.Equal([1, 1, 1, 1], Row(attentionMask, 1));
        Assert.Equal([0, 0, 0, 0], Row(tokenTypeIds, 0));
        Assert.Equal([0, 0, 0, 0], Row(tokenTypeIds, 1));
    }

    [Fact]
    public void BuildInputs_of_equal_length_rows_pads_nothing()
    {
        long[][] ids =
        [
            [101, 5, 102],
            [101, 6, 102],
        ];

        (DenseTensor<long> inputIds, DenseTensor<long> attentionMask, _) =
            EmbeddingBatching.BuildInputs(ids);

        Assert.Equal(new[] { 2, 3 }, inputIds.Dimensions.ToArray());
        Assert.Equal([1, 1, 1], Row(attentionMask, 0));
        Assert.Equal([1, 1, 1], Row(attentionMask, 1));
    }

    [Fact]
    public void MeanPool_averages_only_real_positions_per_row()
    {
        DenseTensor<float> output = Output(
            batches: 2,
            length: 3,
            hidden: 2,
            value: (b, i, j) => (b + 1) * 100 + (i + 1) * 10 + (j + 1));

        float[][] pooled = EmbeddingBatching.MeanPool(output, [3, 1]);

        Assert.Equal(2, pooled.Length);
        Assert.Equal(new[] { 121f, 122f }, pooled[0]);
        Assert.Equal(new[] { 211f, 212f }, pooled[1]);
    }

    [Fact]
    public void MeanPool_of_a_single_row_matches_the_previous_single_chunk_formula()
    {
        DenseTensor<float> output = Output(
            batches: 1,
            length: 3,
            hidden: 2,
            value: (b, i, j) => (i + 1) * 10 + (j + 1));

        float[] pooled = EmbeddingBatching.MeanPool(output, [3]).Single();

        Assert.Equal(new[] { 21f, 22f }, pooled);
    }

    [Fact]
    public void BucketByLength_groups_similar_lengths_together_in_ascending_order()
    {
        // Unordered input: long, short, short, long rows.
        long[] tokens510 = Enumerable.Range(1, 508).Select(i => (long)i).Prepend(101L).Append(102L).ToArray();
        long[][] rows =
        [
            tokens510,     // 510 tokens
            [101, 5, 102], // 3 tokens
            [101, 6, 102], // 3 tokens
            [101, 7, 102], // 3 tokens  (kept near-zero spread so batches pair short with short)
        ];

        List<long[][]> batches = EmbeddingBatching.BucketByLength(rows, batchSize: 2);

        // Length-sorted grouping: the two 3-token rows share a batch and the padded
        // shapes stay small; the 510-token rows only pay padding for themselves.
        Assert.Equal([2, 2], batches.Select(b => b.Length).ToArray());
        Assert.Equal([3, 3], batches[0].Select(r => r.Length).ToArray());
        Assert.Equal([3, 510], batches[1].Select(r => r.Length).ToArray());
    }

    [Fact]
    public void BucketByLength_over_edge_inputs_behaves_like_a_windowed_partition()
    {
        Assert.Empty(EmbeddingBatching.BucketByLength([], batchSize: 4));

        long[] a = [101, 5, 102];
        long[] b = [101, 6, 102];
        long[] c = [101, 7, 102];
        long[] d = [101, 8, 102];
        List<long[][]> exact = EmbeddingBatching.BucketByLength([d, d, d, d], batchSize: 2);
        Assert.Equal(2, exact.Count);
        Assert.All(exact, batch => Assert.Equal(2, batch.Length));

        List<long[][]> partial = EmbeddingBatching.BucketByLength([a, b, c], batchSize: 2);
        Assert.Equal(2, partial.Count);
        Assert.Equal(2, partial[0].Length);
        Assert.Equal(1, partial[1].Length);
        Assert.Equal([3, 3], partial[0].Select(r => r.Length).ToArray());
    }

    private static long[] Row(DenseTensor<long> tensor, int row) =>
        Enumerable.Range(0, tensor.Dimensions[1])
            .Select(i => tensor[row, i])
            .ToArray();

    [Fact]
    public void TruncateToMaxChars_cuts_only_strings_over_the_limit()
    {
        Assert.Equal("abc", EmbeddingBatching.TruncateToMaxChars("abc", maxChars: 5));
        Assert.Equal("abcde", EmbeddingBatching.TruncateToMaxChars("abcde", maxChars: 5));
        Assert.Equal("abcde", EmbeddingBatching.TruncateToMaxChars("abcdefgh", maxChars: 5));
        Assert.Equal("", EmbeddingBatching.TruncateToMaxChars("abc", maxChars: 0));
    }

    private static DenseTensor<float> Output(int batches, int length, int hidden, Func<int, int, int, float> value)
    {
        DenseTensor<float> tensor = new(new[] { batches, length, hidden });
        for (int b = 0; b < batches; b++)
        {
            for (int i = 0; i < length; i++)
            {
                for (int j = 0; j < hidden; j++)
                {
                    tensor[b, i, j] = value(b, i, j);
                }
            }
        }

        return tensor;
    }
}
