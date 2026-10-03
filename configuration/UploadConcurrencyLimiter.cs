using Microsoft.Extensions.Options;

namespace configuration;

public sealed class UploadConcurrencyLimiter(IOptions<RagOptions> options)
{
    private readonly SemaphoreSlim _gate = new(
        options.Value.MaxConcurrentUploads,
        options.Value.MaxConcurrentUploads);

    public bool TryEnter() => _gate.Wait(0);

    public void Exit() => _gate.Release();
}
