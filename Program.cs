using Microsoft.AspNetCore.Mvc;
using chunker;
using vaultReader;
var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();



app.MapPost("/md", async (HttpRequest request) =>
{
    var form = await request.ReadFormAsync();
    
    List<DocumentData> Documents = VaultReader.reader(form.Files);

    // var _chunk = Chunker.Chunking(texts[1], 600);
    // return Results.Ok(_chunk);
    return Results.Ok(Documents);
})
.DisableAntiforgery();

app.MapGet("/siu", () =>
{
    return Results.Ok("siu");
});


app.Run();
