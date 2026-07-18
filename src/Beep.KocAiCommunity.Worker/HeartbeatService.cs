namespace Beep.KocAiCommunity.Worker;

/// <summary>
/// Placeholder background service proving the Worker host runs. Replaced by the run-execution,
/// outbox-dispatch, experiment-sink, and health-monitor services in later phases.
/// </summary>
public sealed class HeartbeatService(ILogger<HeartbeatService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("KocAiCommunity Worker heartbeat at {UtcNow:O}", DateTimeOffset.UtcNow);
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }
}
