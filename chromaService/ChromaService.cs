using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using configuration;
using Microsoft.Extensions.Options;

public record ChromaDocument(
    string Id,
    float[] Embedding,
    string Content,
    Dictionary<string, object> Metadata
);

public record SearchResult(
    string Id,
    string Content,
    Dictionary<string, object> Metadata,
    float Distance
);

public class ChromaService
{
    private readonly HttpClient _httpClient;
    private readonly string _collectionName;
    private readonly string _collectionsPath;
    private readonly ILogger<ChromaService> _logger;
    private string? _collectionId;

    public ChromaService(HttpClient httpClient, IOptions<RagOptions> options, ILogger<ChromaService> logger)
    {
        RagOptions opts = options.Value;
        _httpClient = httpClient;
        _collectionName = opts.CollectionName;
        _logger = logger;
        _collectionsPath = $"/api/v2/tenants/{opts.ChromaTenant}/databases/{opts.ChromaDatabase}/collections";
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        using HttpResponseMessage response = await _httpClient.GetAsync($"{_collectionsPath}/{_collectionName}", ct);

        if (response.IsSuccessStatusCode)
        {
            JsonElement collection = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
            _collectionId = TryGetId(collection);
            return;
        }

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            await CreateCollectionAsync(ct);
            return;
        }

        string body = await response.Content.ReadAsStringAsync(ct);
        throw new HttpRequestException(
            $"Could not retrieve the collection '{_collectionName}': {(int)response.StatusCode} {response.ReasonPhrase}. Response: {body}");
    }

    private async Task CreateCollectionAsync(CancellationToken ct)
    {
        var payload = new
        {
            name = _collectionName,
            metadata = new Dictionary<string, object> { { "hnsw:space", "cosine" } }
        };

        using HttpResponseMessage createResponse = await _httpClient.PostAsJsonAsync(_collectionsPath, payload, ct);

        if (createResponse.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            _logger.LogInformation("The {Name} collection already exists; retrieving id", _collectionName);
            using HttpResponseMessage existing = await _httpClient.GetAsync($"{_collectionsPath}/{_collectionName}", ct);
            existing.EnsureSuccessStatusCode();
            JsonElement doc = await existing.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
            _collectionId = TryGetId(doc);
            return;
        }

        if (!createResponse.IsSuccessStatusCode)
        {
            string body = await createResponse.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException(
                $"Could not create the collection '{_collectionName}': {(int)createResponse.StatusCode} {createResponse.ReasonPhrase}. Response: {body}");
        }

        JsonElement createdCollection = await createResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        _collectionId = TryGetId(createdCollection);
    }

    public async Task AddSessionRecordsAsync(string sessionId, List<ChromaDocument> documents, CancellationToken ct = default)
    {
        EnsureInitialized();

        var ids = new List<string>(documents.Count);
        var embeddings = new List<float[]>(documents.Count);
        var metadatas = new List<Dictionary<string, object>>(documents.Count);
        var contents = new List<string>(documents.Count);

        foreach (ChromaDocument doc in documents)
        {
            ids.Add(doc.Id);
            embeddings.Add(doc.Embedding);
            contents.Add(doc.Content);
            metadatas.Add(new Dictionary<string, object>(doc.Metadata) { ["session_id"] = sessionId });
        }

        var payload = new
        {
            ids,
            embeddings,
            metadatas,
            documents = contents
        };

        using HttpResponseMessage response = await _httpClient.PostAsJsonAsync($"{_collectionsPath}/{_collectionId}/add", payload, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task<List<SearchResult>> QuerySessionAsync(string sessionId, float[] queryEmbedding, int topK, CancellationToken ct = default)
    {
        EnsureInitialized();

        var payload = new
        {
            query_embeddings = new[] { queryEmbedding },
            n_results = topK,
            where = new Dictionary<string, object> { ["session_id"] = sessionId }
        };

        using HttpResponseMessage response = await _httpClient.PostAsJsonAsync($"{_collectionsPath}/{_collectionId}/query", payload, ct);
        response.EnsureSuccessStatusCode();

        JsonElement json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);

        var results = new List<SearchResult>();

        if (!json.TryGetProperty("ids", out JsonElement idsElement) || idsElement.GetArrayLength() == 0)
        {
            return results;
        }

        JsonElement ids = idsElement[0];
        JsonElement docs = json.GetProperty("documents")[0];
        JsonElement metas = json.GetProperty("metadatas")[0];
        JsonElement distances = json.GetProperty("distances")[0];

        for (int i = 0; i < ids.GetArrayLength(); i++)
        {
            Dictionary<string, object> metadataDict =
                JsonSerializer.Deserialize<Dictionary<string, object>>(metas[i].GetRawText())
                ?? new Dictionary<string, object>();

            results.Add(new SearchResult(
                Id: ids[i].GetString() ?? string.Empty,
                Content: docs[i].GetString() ?? string.Empty,
                Metadata: metadataDict,
                Distance: distances[i].GetSingle()
            ));
        }

        return results;
    }

    public async Task TerminateSessionAsync(string sessionId, CancellationToken ct = default)
    {
        EnsureInitialized();

        var payload = new
        {
            where = new Dictionary<string, object> { ["session_id"] = sessionId }
        };

        using HttpResponseMessage response = await _httpClient.PostAsJsonAsync($"{_collectionsPath}/{_collectionId}/delete", payload, ct);
        response.EnsureSuccessStatusCode();
    }

    private void EnsureInitialized()
    {
        if (string.IsNullOrEmpty(_collectionId))
            throw new InvalidOperationException("Debes ejecutar InitializeAsync() antes de operar con ChromaDB.");
    }

    private static string? TryGetId(JsonElement element)
    {
        if (element.TryGetProperty("id", out JsonElement idElement) && idElement.ValueKind == JsonValueKind.String)
        {
            return idElement.GetString();
        }
        return null;
    }
}
