using chunker;
using configuration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using vaultReader;

public static class SessionEndpoints
{
    public static void MapSessionEndpoints(this WebApplication app)
    {
        app.MapPost("/session", (SessionRegistry registry) =>
            Results.Ok(new { sessionId = registry.Create() }));

        app.MapDelete("/session/{id}", async (string id, SessionRegistry registry, IChromaService chroma, ILogger<Endpoints> logger, CancellationToken ct) =>
        {
            if (!registry.Exists(id))
                return Results.NotFound(new { error = "Session not found" });

            await chroma.InitializeAsync(ct);
            await chroma.TerminateSessionAsync(id, ct);
            registry.Remove(id);
            logger.LogInformation("Session {SessionId} terminated", id);
            return Results.Ok(new { deleted = id });
        });

        app.MapPost("/session/{id}/md", async (
            string id,
            SessionRegistry registry,
            IEmbedder embed,
            IChromaService chroma,
            IOptions<RagOptions> options,
            HttpRequest request,
            ILogger<Endpoints> logger,
            CancellationToken ct) =>
        {
            if (!registry.Exists(id))
                return Results.NotFound(new { error = "Session not found" });
            registry.Touch(id);

            RagOptions opts = options.Value;
            IFormCollection form = await request.ReadFormAsync(ct);
            List<DocumentData> documents = await VaultReader.reader(form.Files, options);

            if (documents.Count == 0)
                return Results.BadRequest(new { error = "No actionable documents were found." });

            List<DocumentChunk> chunks = embed
                .EmbeddRange(Chunker.Chunking(documents, opts.ChunkThreshold))
                .ToList();

            chunks = ChunkIdRewriter.RewriteChunkIds(chunks, id);

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
            logger.LogInformation("Session {SessionId} stored {Count} chunks", id, docs.Count);
            return Results.Ok(new { stored = docs.Count });
        })
        .DisableAntiforgery();

        app.MapPost("/session/{id}/query", async (
            string id,
            SessionRegistry registry,
            IEmbedder embed,
            IChromaService chroma,
            IOptions<RagOptions> options,
            QueryRequest body,
            ILogger<Endpoints> logger,
            CancellationToken ct) =>
        {
            if (!registry.Exists(id))
                return Results.NotFound(new { error = "Session not found" });
            registry.Touch(id);

            RagOptions opts = options.Value;
            int topK = body.TopK ?? opts.DefaultTopK;
            if (topK < 1 || topK > opts.MaxTopK)
                return Results.BadRequest(new { error = $"topK must be between 1 and {opts.MaxTopK}" });

            if (string.IsNullOrWhiteSpace(body.Prompt))
                return Results.BadRequest(new { error = "Prompt cannot be empty" });

            await chroma.InitializeAsync(ct);
            float[] queryEmbedding = embed.Embed(body.Prompt);
            List<SearchResult> results = await chroma.QuerySessionAsync(id, queryEmbedding, topK, ct);
            logger.LogInformation("Session {SessionId} query returned {Count} results", id, results.Count);
            return Results.Ok(results);
        });
    }
}

public record QueryRequest(string Prompt, int? TopK);

public sealed class Endpoints;
