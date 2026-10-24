using System.Text.Json;
using configuration;
using Xunit;

namespace ObsidianRAG.Tests.program;

public class AppsettingsParityTests
{
    [Fact]
    public void Every_RagOption_is_listed_in_appsettings()
    {
        string path = Path.Combine(TestSupport.RepoRoot, "appsettings.json");
        Assert.True(File.Exists(path), "appsettings.json must exist at the repo root");

        using JsonDocument appsettings = JsonDocument.Parse(File.ReadAllText(path));
        JsonElement rag = appsettings.RootElement.GetProperty("Rag");

        string[] missing = typeof(RagOptions).GetProperties()
            .Select(property => property.Name)
            .Where(name => !rag.TryGetProperty(name, out _))
            .ToArray();

        Assert.True(missing.Length == 0,
            $"appsettings.json is missing Rag options: {string.Join(", ", missing)}");
    }
}
