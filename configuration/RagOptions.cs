using System.ComponentModel.DataAnnotations;

namespace configuration;

public class RagOptions
{
    public const string SectionName = "Rag";

    [Required]
    public string ModelPath { get; set; } = "model/model.onnx";

    [Required]
    public string VocabPath { get; set; } = "model/vocab.txt";

    [Range(1, 100_000)]
    public int MaxTokenLength { get; set; } = 512;

    [Required]
    public string ChromaBaseUrl { get; set; } = "http://127.0.0.1:8000";

    [Range(1, 600)]
    public int ChromaTimeoutSeconds { get; set; } = 30;

    [Required]
    public string ChromaTenant { get; set; } = "default_tenant";

    [Required]
    public string ChromaDatabase { get; set; } = "default_database";

    [Required]
    public string CollectionName { get; set; } = "vault_collection";

    [Range(1, 1_000_000)]
    public int ChunkThreshold { get; set; } = 600;

    [Range(1, 10_000)]
    public int DefaultTopK { get; set; } = 5;

    [Range(1, 10_000)]
    public int MaxTopK { get; set; } = 50;

    [Range(1, long.MaxValue)]
    public long MaxUploadBytes { get; set; } = 25 * 1024 * 1024;

    [Range(1, long.MaxValue)]
    public long MaxZipEntryBytes { get; set; } = 5 * 1024 * 1024;

    [Range(1, long.MaxValue)]
    public long MaxZipTotalUncompressedBytes { get; set; } = 200L * 1024 * 1024;

    [Range(1, int.MaxValue)]
    public int MaxZipEntries { get; set; } = 5_000;

    [Range(1, 10_000)]
    public int MaxZipCompressionRatio { get; set; } = 100;

    [Range(1, int.MaxValue)]
    public int SessionTtlMinutes { get; set; } = 10;

    [Range(1, int.MaxValue)]
    public int SweepIntervalSeconds { get; set; } = 30;

    [Range(1, int.MaxValue)]
    public int HealthCheckTimeoutMs { get; set; } = 2000;

    public int RecentRequestCapacity { get; set; } = 100;
}
