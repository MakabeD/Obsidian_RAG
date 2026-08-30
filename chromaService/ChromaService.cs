using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

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
    private string? _collectionId;

    public ChromaService(HttpClient httpClient, string collectionName = "vault_collection")
    {
        _httpClient = httpClient;
        _collectionName = collectionName;
    }

    
    private const string CollectionsPath = "/api/v2/tenants/default_tenant/databases/default_database/collections";

    public async Task InitializeAsync()
    {
        var response = await _httpClient.GetAsync($"{CollectionsPath}/{_collectionName}");

        if (response.IsSuccessStatusCode)
        {
            var collection = await response.Content.ReadFromJsonAsync<JsonElement>();
            _collectionId = collection.GetProperty("id").GetString();
            return;
        }

        var createPayload = new
        {
            name = _collectionName,
            metadata = new Dictionary<string, object> { { "hnsw:space", "cosine" } }
        };

        var createResponse = await _httpClient.PostAsJsonAsync(CollectionsPath, createPayload);

        if (createResponse.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            response = await _httpClient.GetAsync($"{CollectionsPath}/{_collectionName}");
            response.EnsureSuccessStatusCode();
            var existing = await response.Content.ReadFromJsonAsync<JsonElement>();
            _collectionId = existing.GetProperty("id").GetString();
            return;
        }

        if (!createResponse.IsSuccessStatusCode)
        {
            string body = await createResponse.Content.ReadAsStringAsync();
            throw new HttpRequestException($"No se pudo crear la colección '{_collectionName}': {(int)createResponse.StatusCode} {createResponse.ReasonPhrase}. Respuesta: {body}");
        }

        var createdCollection = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        _collectionId = createdCollection.GetProperty("id").GetString();
    }


    public async Task AddSessionRecordsAsync(string sessionId, List<ChromaDocument> documents)
    {
        EnsureInitialized();

        var ids = new List<string>();
        var embeddings = new List<float[]>();
        var metadatas = new List<Dictionary<string, object>>();
        var contents = new List<string>();

        foreach (var doc in documents)
        {
            ids.Add(doc.Id);
            embeddings.Add(doc.Embedding);
            contents.Add(doc.Content);

            
            var meta = new Dictionary<string, object>(doc.Metadata)
            {
                ["session_id"] = sessionId
            };
            metadatas.Add(meta);
        }

        var payload = new
        {
            ids,
            embeddings,
            metadatas,
            documents = contents
        };

        var response = await _httpClient.PostAsJsonAsync($"{CollectionsPath}/{_collectionId}/add", payload);
        response.EnsureSuccessStatusCode();
    }


    public async Task<List<SearchResult>> QuerySessionAsync(string sessionId, float[] queryEmbedding, int topK = 5)
    {
        EnsureInitialized();

        var payload = new
        {
            query_embeddings = new[] { queryEmbedding },
            n_results = topK,
            where = new Dictionary<string, object> { ["session_id"] = sessionId }
        };

        var response = await _httpClient.PostAsJsonAsync($"{CollectionsPath}/{_collectionId}/query", payload);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        
        var results = new List<SearchResult>();
        var ids = json.GetProperty("ids")[0];
        var docs = json.GetProperty("documents")[0];
        var metas = json.GetProperty("metadatas")[0];
        var distances = json.GetProperty("distances")[0];
        // TODO: can be this more optimized?
        for (int i = 0; i < ids.GetArrayLength(); i++)
        {
            var metadataDict = JsonSerializer.Deserialize<Dictionary<string, object>>(metas[i].GetRawText()) 
                               ?? new Dictionary<string, object>();

            results.Add(new SearchResult(
                Id: ids[i].GetString()!,
                Content: docs[i].GetString()!,
                Metadata: metadataDict,
                Distance: distances[i].GetSingle()
            ));
        }

        return results;
    }


    public async Task TerminateSessionAsync(string sessionId)
    {
        EnsureInitialized();

        var payload = new
        {
            where = new Dictionary<string, object> { ["session_id"] = sessionId }
        };

        var response = await _httpClient.PostAsJsonAsync($"{CollectionsPath}/{_collectionId}/delete", payload);
        response.EnsureSuccessStatusCode();
    }

    private void EnsureInitialized()
    {
        if (string.IsNullOrEmpty(_collectionId))
            throw new InvalidOperationException("Debes ejecutar InitializeAsync() antes de operar con ChromaDB.");
    }
}