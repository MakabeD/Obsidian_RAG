using System.Net;
using System.Net.Http.Json;
using chunker;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ObsidianRAG.Tests.program;

[Collection(nameof(WebHost))]
public class QueryRequestValidationTests
{
    [Fact]
    public async Task Query_with_empty_prompt_is_rejected_by_the_endpoint_check_with_its_own_error()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                TestSupport.RemoveAll(services, typeof(IEmbedder));
                TestSupport.RemoveAll(services, typeof(IChromaService));
                services.AddSingleton<IEmbedder>(new TestSupport.StubEmbedder());
                services.AddSingleton<IChromaService>(new TestSupport.StubChroma());
            });
        });
        HttpClient client = factory.CreateClient();

        HttpResponseMessage created = await client.PostAsync("/session", content: null);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        TestSupport.SessionResponse? session = await created.Content.ReadFromJsonAsync<TestSupport.SessionResponse>();
        Assert.NotNull(session);

        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/session/{session!.SessionId}/query",
            new { prompt = "", topK = (int?)null });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Prompt cannot be empty", body);
    }

    [Fact]
    public async Task Query_with_a_prompt_over_the_cap_is_rejected_before_any_embedding()
    {
        TestSupport.CountingEmbedder embedder = new();
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Rag:MaxPromptChars", "50");
            builder.ConfigureServices(services =>
            {
                TestSupport.RemoveAll(services, typeof(IEmbedder));
                TestSupport.RemoveAll(services, typeof(IChromaService));
                services.AddSingleton<IEmbedder>(embedder);
                services.AddSingleton<IChromaService>(new TestSupport.StubChroma());
            });
        });
        HttpClient client = factory.CreateClient();

        HttpResponseMessage created = await client.PostAsync("/session", content: null);
        TestSupport.SessionResponse? session = await created.Content.ReadFromJsonAsync<TestSupport.SessionResponse>();
        Assert.NotNull(session);

        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/session/{session!.SessionId}/query",
            new { prompt = new string('a', 51), topK = (int?)null });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        Assert.Contains("too long", body);
        Assert.Equal(0, embedder.EmbedCalls);
        Assert.Equal(0, embedder.RangeCalls);
    }

    [Fact]
    public async Task Query_with_a_prompt_at_exactly_the_cap_is_accepted()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Rag:MaxPromptChars", "50");
            builder.ConfigureServices(services =>
            {
                TestSupport.RemoveAll(services, typeof(IEmbedder));
                TestSupport.RemoveAll(services, typeof(IChromaService));
                services.AddSingleton<IEmbedder>(new TestSupport.StubEmbedder());
                services.AddSingleton<IChromaService>(new TestSupport.StubChroma());
            });
        });
        HttpClient client = factory.CreateClient();

        HttpResponseMessage created = await client.PostAsync("/session", content: null);
        TestSupport.SessionResponse? session = await created.Content.ReadFromJsonAsync<TestSupport.SessionResponse>();
        Assert.NotNull(session);

        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/session/{session!.SessionId}/query",
            new { prompt = new string('a', 50), topK = (int?)null });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
