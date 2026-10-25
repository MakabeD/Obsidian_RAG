using System.Text.RegularExpressions;
using ObsidianRAG.Tests.program;
using Xunit;

namespace ObsidianRAG.Tests.infra;

public class RootComposeTests
{
    private static string RootCompose =>
        File.ReadAllText(Path.Combine(TestSupport.RepoRoot, "docker-compose.yml"));

    private static string ChromaOnlyCompose =>
        File.ReadAllText(Path.Combine(TestSupport.RepoRoot, "chromadb", "docker-compose.yml"));

    [Fact]
    public void The_root_compose_starts_api_and_chroma_together()
    {
        string compose = RootCompose;

        Assert.Contains("services:", compose);
        Assert.Contains("  api:", compose);
        Assert.Contains("  chromadb:", compose);
    }

    [Fact]
    public void The_chroma_image_matches_the_chromadb_only_compose()
    {
        string rootDigest = Regex.Match(RootCompose, @"chroma@(sha256:[0-9a-f]{64})").Groups[1].Value;
        string chromaOnlyDigest = Regex.Match(ChromaOnlyCompose, @"chroma@(sha256:[0-9a-f]{64})").Groups[1].Value;

        Assert.False(string.IsNullOrEmpty(chromaOnlyDigest), "chromadb/docker-compose.yml pins a digest");
        Assert.Equal(chromaOnlyDigest, rootDigest);
    }

    [Fact]
    public void Every_published_host_port_stays_bound_to_localhost()
    {
        MatchCollection mappings = Regex.Matches(RootCompose, @"""([^""]*:\d+)""");

        Assert.NotEmpty(mappings);
        Assert.All(mappings, m =>
            Assert.StartsWith("127.0.0.1:", m.Groups[1].Value));

        Assert.Contains("\"127.0.0.1:5011:8080\"", RootCompose);
        Assert.Contains("\"127.0.0.1:8000:8000\"", RootCompose);
    }

    [Fact]
    public void The_api_reaches_chroma_through_the_internal_network()
    {
        Assert.Contains("Rag__ChromaBaseUrl=http://chromadb:8000", RootCompose);
    }

    [Fact]
    public void The_api_waits_for_a_healthy_chroma()
    {
        Assert.Contains("condition: service_healthy", RootCompose);
    }

    [Fact]
    public void The_chroma_healthcheck_probes_the_real_http_endpoint()
    {
        Assert.Contains("/api/v2/heartbeat", RootCompose);
    }
}
