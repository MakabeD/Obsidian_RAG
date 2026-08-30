public class SessionSweeper(SessionRegistry registry, ChromaService chroma, ILogger<SessionSweeper> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new(Interval);
        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            foreach (string sessionId in registry.GetExpired())
            {
                try
                {
                    await chroma.InitializeAsync();
                    await chroma.TerminateSessionAsync(sessionId);
                    registry.Remove(sessionId);
                    logger.LogInformation("Sesion {SessionId} expirada y eliminada de Chroma", sessionId);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Could not clean up expired session {SessionId}; retrying in the next cycle", sessionId);
                }
            }
        }
    }
}
