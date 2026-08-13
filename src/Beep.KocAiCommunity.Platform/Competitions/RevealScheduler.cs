using Beep.KocAiCommunity.Application.Competitions;

namespace Beep.KocAiCommunity.Platform.Competitions;

/// <summary>
/// Concludes competitions whose reveal moment has passed with <c>ConcludeAtReveal</c> set.
/// <para>
/// A host who sets a reveal date usually means "this is when it ends" — but nothing used to happen at
/// that moment unless they came back at the right hour and pressed Conclude by hand. This tick calls
/// the same conclusion path the button does (<see cref="ICompetitionService.ConcludeDueAsync"/>), so
/// notifications and podium awards cannot differ by who closed the competition.
/// </para>
/// <para>
/// One-minute cadence: a countdown that reads 00:00 while submissions stay open for another ten
/// minutes reads as broken. Sub-minute precision buys nothing — the reveal is a date-and-time picker,
/// not a starting gun.
/// </para>
/// </summary>
public sealed class RevealScheduler(IServiceScopeFactory scopeFactory, ILogger<RevealScheduler> logger)
    : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var competitions = scope.ServiceProvider.GetRequiredService<ICompetitionService>();
                var concluded = await competitions.ConcludeDueAsync(stoppingToken);
                if (concluded > 0)
                {
                    logger.LogInformation("Reveal scheduler concluded {Count} competition(s).", concluded);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Reveal scheduler tick failed");
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
