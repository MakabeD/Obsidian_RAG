using configuration;
using Xunit;

namespace ObsidianRAG.Tests.program;

public class ConfigurationDocsTests
{
    [Fact]
    public void Every_RagOption_is_documented_in_docs_configuration()
    {
        string path = Path.Combine(TestSupport.RepoRoot, "docs", "configuration.md");
        Assert.True(File.Exists(path), "docs/configuration.md must exist");

        string docs = File.ReadAllText(path);

        string[] missing = typeof(RagOptions).GetProperties()
            .Select(property => property.Name)
            .Where(name => !docs.Contains(name))
            .ToArray();

        Assert.True(missing.Length == 0,
            $"docs/configuration.md is missing Rag options: {string.Join(", ", missing)}");
    }
}
