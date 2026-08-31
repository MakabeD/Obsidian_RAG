using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using chunker;
using configuration;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using vaultReader;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddOptions<RagOptions>()
    .Bind(builder.Configuration.GetSection(RagOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

RagOptions rag = builder.Configuration
    .GetSection(RagOptions.SectionName)
    .Get<RagOptions>() ?? new RagOptions();

builder.Services.AddSingleton<EmbeddingService>();
builder.Services.AddSingleton<SessionRegistry>();
builder.Services.AddHostedService<SessionSweeper>();

builder.Services.AddHttpClient<ChromaService>(c =>
{
    c.BaseAddress = new Uri(rag.ChromaBaseUrl);
    c.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.Configure<FormOptions>(o =>
{
    o.MultipartBodyLengthLimit = rag.MaxUploadBytes;
});
builder.Services.Configure<Microsoft.AspNetCore.Server.Kestrel.Core.KestrelServerOptions>(o =>
{
    o.Limits.MaxRequestBodySize = rag.MaxUploadBytes;
});

WebApplication app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.MapGet("/siu", () => Results.Ok(new { status = "ok" }));

app.MapPost("/session", (SessionRegistry registry) =>
    Results.Ok(new { sessionId = registry.Create() }));

app.MapDelete("/session/{id}", async (string id, SessionRegistry registry, ChromaService chroma, CancellationToken ct) =>
{
    if (!registry.Exists(id))
        return Results.NotFound(new { error = "Session not found" });

    await chroma.InitializeAsync(ct);
    await chroma.TerminateSessionAsync(id, ct);
    registry.Remove(id);
    return Results.Ok(new { deleted = id });
});

app.MapPost("/session/{id}/md", async (
    string id,
    SessionRegistry registry,
    EmbeddingService embed,
    ChromaService chroma,
    IOptions<RagOptions> options,
    HttpRequest request,
    CancellationToken ct) =>
{
    if (!registry.Exists(id))
        return Results.NotFound(new { error = "Sesion no encontrada" });
    registry.Touch(id);

    RagOptions opts = options.Value;
    IFormCollection form = await request.ReadFormAsync(ct);
    List<DocumentData> documents = await VaultReader.reader(form.Files, options);

    if (documents.Count == 0)
        return Results.BadRequest(new { error = "No se encontraron documentos procesables" });

    List<DocumentChunk> chunks = embed
        .EmbeddRange(Chunker.Chunking(documents, opts.ChunkThreshold))
        .ToList();

    chunks = RewriteChunkIds(chunks, id);

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

    await chroma.InitializeAsync(ct);
    await chroma.AddSessionRecordsAsync(id, docs, ct);
    return Results.Ok(new { stored = docs.Count });
})
.DisableAntiforgery();

app.MapPost("/session/{id}/query", async (
    string id,
    SessionRegistry registry,
    EmbeddingService embed,
    ChromaService chroma,
    IOptions<RagOptions> options,
    QueryRequest body,
    CancellationToken ct) =>
{
    if (!registry.Exists(id))
        return Results.NotFound(new { error = "Sesion no encontrada" });
    registry.Touch(id);

    RagOptions opts = options.Value;
    int topK = body.TopK ?? opts.DefaultTopK;
    if (topK < 1 || topK > opts.MaxTopK)
        return Results.BadRequest(new { error = $"topK debe estar entre 1 y {opts.MaxTopK}" });

    if (string.IsNullOrWhiteSpace(body.Prompt))
        return Results.BadRequest(new { error = "El prompt no puede estar vacío" });

    await chroma.InitializeAsync(ct);
    float[] queryEmbedding = embed.Embed(body.Prompt);
    List<SearchResult> results = await chroma.QuerySessionAsync(id, queryEmbedding, topK, ct);
    return Results.Ok(results);
});

app.Run();

static List<DocumentChunk> RewriteChunkIds(List<DocumentChunk> chunks, string sessionId)
{
    string prefix = $"{sessionId}_";
    for (int i = 0; i < chunks.Count; i++)
    {
        DocumentChunk c = chunks[i];
        string contentHash = ShortHash(c.Content);
        string safeName = SanitizeForId(c.Metadata?.FileName ?? "chunk");
        c.Id = $"{prefix}{safeName}_{contentHash}_{i:D4}";
    }
    return chunks;
}

static string SanitizeForId(string s)
{
    StringBuilder sb = new(s.Length);
    foreach (char ch in s)
    {
        sb.Append(char.IsLetterOrDigit(ch) || ch is '-' or '_' or '.' ? ch : '_');
    }
    return sb.ToString();
}

static string ShortHash(string content)
{
    byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(content));
    return Convert.ToHexString(hash, 0, 4).ToLowerInvariant();
}

public record QueryRequest([Required] string Prompt, int? TopK);
