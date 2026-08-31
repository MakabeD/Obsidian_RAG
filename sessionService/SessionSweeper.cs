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
                    logger.LogInformation("Sesion {SessionId} expirada y eliminada de Chroma", sessionId);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "No se pudo limpiar la sesion expirada {SessionId}, se reintenta en el proximo ciclo", sessionId);
                }
            }
        }
    }
}
