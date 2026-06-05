using Microsoft.AspNetCore.Mvc;
using obsidian_RAG;

var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();



app.MapPost("/md", async (HttpRequest request) =>
{
    var form = await request.ReadFormAsync();
    var texts = filetostring(form.Files);
    var _chunk = Chunk.Chunker(texts[1], 600);
    return Results.Ok(_chunk);
})
.DisableAntiforgery();

app.MapGet("/siu", () =>
{
    return Results.Ok("siu");
});

List<string> filetostring(IFormFileCollection files)
{
    List<string> texts = new List<string>();
    foreach (var file in files)
    {
        var stream = file.OpenReadStream();
        var reader = new StreamReader(stream);
        texts.Add(file.FileName.ToString()+"\n"+reader.ReadToEnd());
    }
    return texts;

}
app.Run();
