using System.Net;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ObsidianRAG.Tests.program;

public class QueryRequestValidationTests
{
    [Fact]
    public async Task Query_with_empty_prompt_is_rejected_by_the_endpoint_check_with_its_own_error()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                RemoveAll(services, typeof(EmbeddingService));
                services.AddSingleton<EmbeddingService>(_ =>
                    (EmbeddingService)RuntimeHelpers.GetUninitializedObject(typeof(EmbeddingService)));
            });
        });
        HttpClient client = factory.CreateClient();

        HttpResponseMessage created = await client.PostAsync("/session", content: null);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        SessionResponse? session = await created.Content.ReadFromJsonAsync<SessionResponse>();
        Assert.NotNull(session);

        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/session/{session!.SessionId}/query",
            new { prompt = "", topK = (int?)null });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Prompt cannot be empty", body);
    }

    private static void RemoveAll(IServiceCollection services, Type t)
    {
        for (int i = services.Count - 1; i >= 0; i--)
        {
            if (services[i].ServiceType == t)
                services.RemoveAt(i);
        }
    }

    private sealed record SessionResponse(string SessionId);
}