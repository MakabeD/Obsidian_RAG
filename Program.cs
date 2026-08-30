using Microsoft.AspNetCore.Mvc;
using chunker;
using vaultReader;
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(
    new EmbeddingService("model/model.onnx", "model/vocab.txt"));

builder.Services.AddHttpClient<ChromaService>(c =>
    c.BaseAddress = new Uri("http://localhost:8000"));

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

app.Run();
