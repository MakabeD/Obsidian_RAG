using obsidian_RAG;

var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();



app.MapPost("/md",  async (List<IFormFile> files) =>
{
    try {

        if (files==null||files.Count==0)
        {
            return Results.BadRequest("No hay archivos");
        }
        List<string> texts=filetostring(files);
        return texts[1];
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message);
    }

}).DisableAntiforgery();


List<string> filetostring(List<IFormFile> files)
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
