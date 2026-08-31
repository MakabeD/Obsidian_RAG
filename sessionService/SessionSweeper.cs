using configuration;
using Microsoft.Extensions.Options;

public class SessionSweeper(SessionRegistry registry, ChromaService chroma, IOptions<RagOptions> options, ILogger<SessionSweeper> logger) : BackgroundService
{
    private readonly TimeSpan _interval = TimeSpan.FromSeconds(Math.Max(5, options.Value.SweepIntervalSeconds));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new(_interval);
        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            foreach (string sessionId in registry.GetExpired())
            {
                try
                {
                    await chroma.InitializeAsync(stoppingToken);
                    await chroma.TerminateSessionAsync(sessionId, stoppingToken);
                    registry.Remove(sessionId);
                    logger.LogInformation("Session {SessionId} expired and removed from Chroma", sessionId);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Could not clean up expired session {SessionId}; retrying in the next cycle.", sessionId);
                }
            }
        }
    }
}
