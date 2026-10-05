using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ObsidianRAG.Tests.program;

[Collection(nameof(WebHost))]
public class UploadConcurrencyTests
{
    [Fact]
    public async Task An_upload_arriving_while_another_is_stored_is_rejected_with_503()
    {
        GatedChroma chroma = new();
        using WebApplicationFactory<Program> factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("Rag:MaxConcurrentUploads", "1");
                builder.ConfigureServices(services =>
                {
                    TestSupport.RemoveAll(services, typeof(IEmbedder));
                    TestSupport.RemoveAll(services, typeof(IChromaService));
                    services.AddSingleton<IEmbedder>(new TestSupport.StubEmbedder());
                    services.AddSingleton<IChromaService>(chroma);
                });
            });
        HttpClient client = factory.CreateClient();

        HttpResponseMessage created = await client.PostAsync("/session", content: null);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        TestSupport.SessionResponse? session = await created.Content.ReadFromJsonAsync<TestSupport.SessionResponse>();
        Assert.NotNull(session);

        Task<HttpResponseMessage> first = client.PostAsync(
            $"/session/{session!.SessionId}/md",
            UploadForm(("a.md", new string('a', 120))));

        await Task.WhenAny(chroma.Entered.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.True(chroma.Entered.Task.IsCompleted, "first upload never reached the Chroma add within 5s");

        HttpResponseMessage second = await client.PostAsync(
            $"/session/{session.SessionId}/md",
            UploadForm(("b.md", new string('b', 120))));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, second.StatusCode);
        string body = await second.Content.ReadAsStringAsync();
        Assert.Contains("Concurrent upload limit reached", body);

        chroma.Release.TrySetResult();
        HttpResponseMessage finished = await first;
        Assert.Equal(HttpStatusCode.OK, finished.StatusCode);
    }

    [Fact]
    public async Task The_slot_is_free_once_the_in_flight_upload_is_finished()
    {
        GatedChroma chroma = new();
        using WebApplicationFactory<Program> factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("Rag:MaxConcurrentUploads", "1");
                builder.ConfigureServices(services =>
                {
                    TestSupport.RemoveAll(services, typeof(IEmbedder));
                    TestSupport.RemoveAll(services, typeof(IChromaService));
                    services.AddSingleton<IEmbedder>(new TestSupport.StubEmbedder());
                    services.AddSingleton<IChromaService>(chroma);
                });
            });
        HttpClient client = factory.CreateClient();

        HttpResponseMessage created = await client.PostAsync("/session", content: null);
        TestSupport.SessionResponse? session = await created.Content.ReadFromJsonAsync<TestSupport.SessionResponse>();
        Assert.NotNull(session);

        Task<HttpResponseMessage> first = client.PostAsync(
            $"/session/{session!.SessionId}/md",
            UploadForm(("a.md", new string('a', 120))));

        await Task.WhenAny(chroma.Entered.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.True(chroma.Entered.Task.IsCompleted, "first upload never reached the Chroma add within 5s");
        chroma.Release.TrySetResult();
        HttpResponseMessage finished = await first;
        Assert.Equal(HttpStatusCode.OK, finished.StatusCode);

        HttpResponseMessage next = await client.PostAsync(
            $"/session/{session.SessionId}/md",
            UploadForm(("c.md", new string('c', 120))));

        Assert.Equal(HttpStatusCode.OK, next.StatusCode);
        Assert.Equal(2, chroma.AddCalls);
    }

    private static MultipartFormDataContent UploadForm(params (string Name, string Content)[] files)
    {
        MultipartFormDataContent form = new();
        foreach ((string name, string content) in files)
        {
            form.Add(
                new StringContent(content, Encoding.UTF8, new MediaTypeHeaderValue("text/markdown")),
                "files",
                name);
        }

        return form;
    }

    // The hold must self-release: a client-side cancel of a held request deadlocks the TestHost.
    private static readonly TimeSpan HoldSafetyTimeout = TimeSpan.FromSeconds(15);

    private sealed class GatedChroma : IChromaService
    {
        public int AddCalls;

        public TaskCompletionSource Entered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Release { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task InitializeAsync(CancellationToken ct = default) => Task.CompletedTask;

        public async Task AddSessionRecordsAsync(string sessionId, List<ChromaDocument> documents, CancellationToken ct = default)
        {
            Interlocked.Increment(ref AddCalls);
            Entered.TrySetResult();
            await Task.WhenAny(Release.Task, Task.Delay(HoldSafetyTimeout));
        }

        public Task<List<SearchResult>> QuerySessionAsync(string sessionId, float[] queryEmbedding, int topK, CancellationToken ct = default)
            => Task.FromResult(new List<SearchResult>());

        public Task TerminateSessionAsync(string sessionId, CancellationToken ct = default) => Task.CompletedTask;
    }
}
