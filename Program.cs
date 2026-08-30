using Microsoft.AspNetCore.Mvc;
using chunker;
using vaultReader;
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(
    new EmbeddingService("model/model.onnx", "model/vocab.txt"));

builder.Services.AddHttpClient<ChromaService>(c =>
    c.BaseAddress = new Uri("http://127.0.0.1:8000"));

builder.Services.AddSingleton<SessionRegistry>();
builder.Services.AddHostedService<SessionSweeper>();

var app = builder.Build();



app.MapPost("/md", async (EmbeddingService embed, ChromaService chroma, HttpRequest request) =>
{
    var form = await request.ReadFormAsync();

    List<DocumentData> Documents = await VaultReader.reader(form.Files);
    IEnumerable<DocumentChunk> chunks = Chunker.Chunking(Documents, 600);
    await chroma.InitializeAsync();
    return Results.Ok(embed.EmbeddRange(chunks));
})
.DisableAntiforgery();

app.MapGet("/siu", () =>
{
    return Results.Ok("siu");
});

app.MapPost("/session", (SessionRegistry registry) =>
{
    return Results.Ok(new { sessionId = registry.Create() });
});

app.MapPost("/session/{id}/md", async (string id, SessionRegistry registry, EmbeddingService embed, ChromaService chroma, HttpRequest request) =>
{
    if (!registry.Exists(id)) return Results.NotFound(new { error = "Sesion no encontrada" });
    registry.Touch(id);

    var form = await request.ReadFormAsync();
    List<DocumentData> documents = await VaultReader.reader(form.Files);
    List<DocumentChunk> chunks = embed.EmbeddRange(Chunker.Chunking(documents, 600)).ToList();

    List<ChromaDocument> docs = chunks.Select(c => new ChromaDocument(
        Id: c.Id,
        Embedding: c.Embedding!,
        Content: c.Content,
        Metadata: new Dictionary<string, object>
        {
            ["source"] = c.Metadata?.Source ?? "",
            ["file_name"] = c.Metadata?.FileName ?? ""
        }
    )).ToList();

    await chroma.InitializeAsync();
    await chroma.AddSessionRecordsAsync(id, docs);
    return Results.Ok(new { stored = docs.Count });
})
.DisableAntiforgery();

app.MapPost("/session/{id}/query", async (string id, SessionRegistry registry, EmbeddingService embed, ChromaService chroma, QueryRequest body) =>
{
    if (!registry.Exists(id)) return Results.NotFound(new { error = "Sesion no encontrada" });
    registry.Touch(id);

    await chroma.InitializeAsync();
    float[] queryEmbedding = embed.Embed(body.Prompt);
    List<SearchResult> results = await chroma.QuerySessionAsync(id, queryEmbedding, body.TopK ?? 5);
    return Results.Ok(results);
});

app.MapDelete("/session/{id}", async (string id, SessionRegistry registry, ChromaService chroma) =>
{
    if (!registry.Exists(id)) return Results.NotFound(new { error = "Sesion no encontrada" });

    await chroma.InitializeAsync();
    await chroma.TerminateSessionAsync(id);
    registry.Remove(id);
    return Results.Ok(new { deleted = id });
});

app.Run();

public record QueryRequest(string Prompt, int? TopK);
