using Microsoft.AspNetCore.Mvc;
using chunker;
using vaultReader;
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(
    new EmbeddingService("model/model.onnx", "model/vocab.txt"));

var app = builder.Build();



app.MapPost("/md", async (EmbeddingService embed, HttpRequest request) =>
{
    var form = await request.ReadFormAsync();
    
    List<DocumentData> Documents = await VaultReader.reader(form.Files);
    IEnumerable<DocumentChunk> chunks = Chunker.Chunking(Documents, 600);
    HttpClient a= new HttpClient{BaseAddress= new Uri("http://localhost:8000")};
    ChromaService service= new ChromaService(a);
    await service.InitializeAsync();
    return Results.Ok(embed.EmbeddRange(chunks));
})
.DisableAntiforgery();

app.MapGet("/siu", () =>
{
    return Results.Ok("siu");
});

app.Run();
