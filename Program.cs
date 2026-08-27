using Microsoft.AspNetCore.Mvc;
using chunker;
using vaultReader;
var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();



app.MapPost("/md", async (HttpRequest request) =>
{
    var form = await request.ReadFormAsync();
    
    List<DocumentData> Documents = await VaultReader.reader(form.Files);

    var x = Chunker.Chunking(Documents[0], 600);
    var embed=new EmbeddingService("model/model.onnx", "model/vocab.txt");
    
    return Results.Ok(embed.Embed(Documents[0].Content));
})
.DisableAntiforgery();

app.MapGet("/siu", () =>
{
    return Results.Ok("siu");
});

app.Run();
