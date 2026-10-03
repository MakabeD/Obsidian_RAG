using chunker;
using configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using vaultReader;

public static class SessionEndpoints
{
    public static void MapSessionEndpoints(this WebApplication app)
    {
        app.MapPost("/session", (SessionRegistry registry) =>
            registry.TryCreate(out string sessionId)
                ? Results.Ok(new { sessionId })
                : Results.Json(
                    new { error = "Session capacity reached; try again later." },
                    statusCode: StatusCodes.Status503ServiceUnavailable));

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

        UploadConcurrencyLimiter uploads = app.Services.GetRequiredService<UploadConcurrencyLimiter>();

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
            if (!registry.Touch(id))
                return Results.NotFound(new { error = "Session not found" });

            RagOptions opts = options.Value;
            IFormCollection form = await request.ReadFormAsync(ct);

            (int documentCount, List<DocumentChunk> chunks) =
                ChunkUpload(await VaultReader.reader(form.Files, options, ct), opts.ChunkThreshold);

            if (documentCount == 0)
                return Results.BadRequest(new { error = "No actionable documents were found." });

            if (chunks.Count > opts.MaxChunkCount)
            {
                return Results.BadRequest(new
                {
                    error = $"Upload produces {chunks.Count} chunks, exceeding the limit of {opts.MaxChunkCount}. Split the vault into smaller uploads."
                });
            }

            chunks = embed
                .EmbeddRange(chunks, ct)
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

            if (!registry.Touch(id))
            {
                return Results.NotFound(new { error = "Session expired during upload; start a new session." });
            }

            await chroma.InitializeAsync(ct);
            await chroma.AddSessionRecordsAsync(id, docs, ct);
            logger.LogInformation("Session {SessionId} stored {Count} chunks", id, docs.Count);
            return Results.Ok(new { stored = docs.Count });
        })
        .AddEndpointFilter(async (ctx, next) =>
        {
            if (!uploads.TryEnter())
            {
                return Results.Json(
                    new { error = "Concurrent upload limit reached; try again shortly." },
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            try
            {
                return await next(ctx);
            }
            finally
            {
                uploads.Exit();
            }
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
            if (!registry.Touch(id))
                return Results.NotFound(new { error = "Session not found" });

            RagOptions opts = options.Value;
            int topK = body.TopK ?? opts.DefaultTopK;
            if (topK < 1 || topK > opts.MaxTopK)
                return Results.BadRequest(new { error = $"topK must be between 1 and {opts.MaxTopK}" });

            if (string.IsNullOrWhiteSpace(body.Prompt))
                return Results.BadRequest(new { error = "Prompt cannot be empty" });

            if (body.Prompt.Length > opts.MaxPromptChars)
            {
                return Results.BadRequest(new
                {
                    error = $"Prompt is too long: {body.Prompt.Length} characters, the limit is {opts.MaxPromptChars}."
                });
            }

            await chroma.InitializeAsync(ct);
            float[] queryEmbedding = embed.Embed(body.Prompt, ct);
            List<SearchResult> results = await chroma.QuerySessionAsync(id, queryEmbedding, topK, ct);
            logger.LogInformation("Session {SessionId} query returned {Count} results", id, results.Count);
            return Results.Ok(results);
        });
    }

    private static (int DocumentCount, List<DocumentChunk> Chunks) ChunkUpload(
        List<DocumentData> documents,
        int chunkThreshold) =>
        (documents.Count, [.. Chunker.Chunking(documents, chunkThreshold)]);
}

public record QueryRequest(string Prompt, int? TopK);

public sealed class Endpoints;
