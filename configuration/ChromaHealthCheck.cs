using System.Net;
using configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace configuration;

public class ChromaHealthCheck(IHttpClientFactory httpClientFactory, IOptions<RagOptions> options, ILogger<ChromaHealthCheck> logger) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        RagOptions opts = options.Value;
        HttpClient client = httpClientFactory.CreateClient(nameof(ChromaHealthCheck));
        if (client.BaseAddress is null)
        {
            client.BaseAddress = new Uri(opts.ChromaBaseUrl);
        }
        client.Timeout = TimeSpan.FromMilliseconds(opts.HealthCheckTimeoutMs);

        try
        {
            using HttpResponseMessage response = await client.GetAsync("/api/v2/heartbeat", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return HealthCheckResult.Healthy("Chroma reachable");
            }
            return HealthCheckResult.Unhealthy($"Chroma heartbeat returned {(int)response.StatusCode} {response.ReasonPhrase}");
        }
        catch (TaskCanceledException)
        {
            logger.LogWarning("Chroma health check timed out after {TimeoutMs}ms", opts.HealthCheckTimeoutMs);
            return HealthCheckResult.Unhealthy("Chroma health check timed out");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Chroma health check failed");
            return HealthCheckResult.Unhealthy("Chroma unreachable", ex);
        }
    }
}
