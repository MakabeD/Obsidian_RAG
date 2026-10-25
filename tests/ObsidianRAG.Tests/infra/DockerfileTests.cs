using System.Text.RegularExpressions;
using ObsidianRAG.Tests.program;
using Xunit;

namespace ObsidianRAG.Tests.infra;

public class DockerfileTests
{
    private static string Dockerfile =>
        File.ReadAllText(Path.Combine(TestSupport.RepoRoot, "Dockerfile"));

    private static MatchCollection FromStages() =>
        Regex.Matches(Dockerfile, @"^FROM\s+(\S+)", RegexOptions.Multiline);

    [Fact]
    public void Dockerfile_is_multi_stage_with_sdk_build_and_aspnet_runtime()
    {
        MatchCollection froms = FromStages();

        Assert.True(froms.Count >= 2, "expected at least a build stage and a final stage");

        Assert.Contains(froms, m => m.Groups[1].Value.Contains("dotnet/sdk"));
        Assert.Contains(froms, m => m.Groups[1].Value.Contains("dotnet/aspnet"));
    }

    [Fact]
    public void Base_images_are_digest_pinned()
    {
        MatchCollection froms = FromStages();

        Assert.NotEmpty(froms);
        Assert.All(froms, m =>
            Assert.Matches(@"@sha256:[0-9a-f]{64}", m.Groups[1].Value));
    }

    [Fact]
    public void The_baked_model_matches_the_pinned_model_in_ci()
    {
        string ci = File.ReadAllText(Path.Combine(TestSupport.RepoRoot, ".github", "workflows", "ci.yml"));
        Match commit = Regex.Match(ci, @"huggingface\.co/\S+/resolve/([0-9a-f]{40})/");
        Match checksum = Regex.Match(ci, @"echo ""([0-9a-f]{64})\s+model/model\.onnx""");

        Assert.True(commit.Success, "ci.yml must pin the HF commit");
        Assert.True(checksum.Success, "ci.yml must pin the model checksum");

        Assert.Contains(commit.Groups[1].Value, Dockerfile);
        Assert.Contains(checksum.Groups[1].Value, Dockerfile);
    }

    [Fact]
    public void The_model_download_is_checksum_verified_in_the_image()
    {
        Assert.Contains("sha256sum -c -", Dockerfile);
    }

    [Fact]
    public void The_container_runs_as_non_root()
    {
        string dockerfile = Dockerfile;

        Assert.Contains("USER app", dockerfile);
        Assert.DoesNotContain("USER root", dockerfile);
    }

    [Fact]
    public void The_healthcheck_probes_the_live_endpoint_over_http()
    {
        string dockerfile = Dockerfile;

        Assert.Contains("/healthz/live", dockerfile);
        Assert.Contains("/dev/tcp", dockerfile);
    }

    [Fact]
    public void The_build_context_excludes_local_artifacts()
    {
        string dockerignore = File.ReadAllText(Path.Combine(TestSupport.RepoRoot, ".dockerignore"));

        Assert.Contains("model/model.onnx", dockerignore);
        Assert.Contains("chromadb/data", dockerignore);
    }
}
