using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();



app.MapPost("/md", async (HttpRequest request) =>
{
    var form = await request.ReadFormAsync();
    var texts = filetostring(form.Files);
    return Results.Ok(texts);
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
        texts.Add(reader.ReadToEnd());
    }
    return texts;

}
app.Run();
