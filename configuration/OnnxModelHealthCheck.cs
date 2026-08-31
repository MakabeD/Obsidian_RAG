using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace configuration;

public class OnnxModelHealthCheck(EmbeddingService embedding) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(embedding.IsLoaded
            ? HealthCheckResult.Healthy("Embedding model loaded")
            : HealthCheckResult.Unhealthy("Embedding model is not loaded"));
    }
}
