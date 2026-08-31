namespace configuration;

public class RagOptions
{
    public const string SectionName = "Rag";

    public string ModelPath { get; set; } = "model/model.onnx";
    public string VocabPath { get; set; } = "model/vocab.txt";
    public int MaxTokenLength { get; set; } = 512;

    public string ChromaBaseUrl { get; set; } = "http://127.0.0.1:8000";
    public string ChromaTenant { get; set; } = "default_tenant";
    public string ChromaDatabase { get; set; } = "default_database";
    public string CollectionName { get; set; } = "vault_collection";

    public int ChunkThreshold { get; set; } = 600;
    public int DefaultTopK { get; set; } = 5;
    public int MaxTopK { get; set; } = 50;

    public long MaxUploadBytes { get; set; } = 25 * 1024 * 1024;
    public long MaxZipEntryBytes { get; set; } = 5 * 1024 * 1024;

    public int SessionTtlMinutes { get; set; } = 10;
    public int SweepIntervalSeconds { get; set; } = 30;
}
