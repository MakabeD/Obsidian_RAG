using System.Net;
using System.Net.Http;
using configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace ObsidianRAG.Tests.configuration;

public class ChromaHealthCheckTests
{
    [Fact]
    public async Task Repeated_invocations_do_not_mutate_the_shared_http_client_timeout()
    {
        var handler = new StubHandler(HttpStatusCode.OK);
        var factory = new SingleClientFactory(handler)
        {
            BaseAddress = new Uri("http://127.0.0.1:65535"),
        };

        var options = Options.Create(new RagOptions
        {
            ChromaBaseUrl = "http://127.0.0.1:65535",
            HealthCheckTimeoutMs = 1234,
        });

        var check = new ChromaHealthCheck(factory, options, NullLogger<ChromaHealthCheck>.Instance);

        HealthCheckResult first = await check.CheckHealthAsync(new HealthCheckContext());
        HealthCheckResult second = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, first.Status);
        Assert.Equal(HealthStatus.Healthy, second.Status);
        Assert.NotEqual(TimeSpan.FromMilliseconds(1234), factory.Client.Timeout);
    }

    [Fact]
    public async Task Returns_unhealthy_when_the_health_check_times_out()
    {
        var handler = new DelayedHandler(TimeSpan.FromSeconds(2));
        var factory = new SingleClientFactory(handler)
        {
            BaseAddress = new Uri("http://127.0.0.1:65535"),
        };

        var options = Options.Create(new RagOptions
        {
            ChromaBaseUrl = "http://127.0.0.1:65535",
            HealthCheckTimeoutMs = 50,
        });

        var check = new ChromaHealthCheck(factory, options, NullLogger<ChromaHealthCheck>.Instance);

        HealthCheckResult result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Equal("Chroma health check timed out", result.Description);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        public StubHandler(HttpStatusCode status) => _status = status;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(_status));
    }

    private sealed class DelayedHandler : HttpMessageHandler
    {
        private readonly TimeSpan _delay;
        public DelayedHandler(TimeSpan delay) => _delay = delay;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(_delay, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    private sealed class SingleClientFactory : IHttpClientFactory
    {
        public HttpClient Client { get; }
        public SingleClientFactory(HttpMessageHandler handler) => Client = new HttpClient(handler);
        public Uri? BaseAddress
        {
            get => Client.BaseAddress;
            set => Client.BaseAddress = value;
        }
        public HttpClient CreateClient(string name) => Client;
    }
}
