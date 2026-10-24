using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace ObsidianRAG.Tests.program;

[Collection(nameof(WebHost))]
public class OpenApiTests
{
    [Fact]
    public async Task OpenAPI_contract_is_served_and_covers_the_session_endpoints()
    {
        using WebApplicationFactory<Program> factory = new();
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement paths = document.RootElement.GetProperty("paths");

        Assert.True(paths.TryGetProperty("/session", out _), "expected /session in the contract");
        Assert.True(paths.TryGetProperty("/session/{id}", out _), "expected /session/{id} in the contract");
        Assert.True(paths.TryGetProperty("/session/{id}/md", out _), "expected /session/{id}/md in the contract");
        Assert.True(paths.TryGetProperty("/session/{id}/query", out _), "expected /session/{id}/query in the contract");
    }

    [Fact]
    public async Task Scalar_UI_is_served_for_the_contract()
    {
        using WebApplicationFactory<Program> factory = new();
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/scalar/v1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
    }
}
