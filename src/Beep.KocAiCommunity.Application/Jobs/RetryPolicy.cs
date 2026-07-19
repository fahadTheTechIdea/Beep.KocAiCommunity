namespace Beep.KocAiCommunity.Application.Jobs;

/// <summary>
/// Pure exponential backoff with a cap. Jitter is applied by the caller (kept out of here so the
/// schedule is deterministic and unit-testable).
/// </summary>
public static class RetryPolicy
{
    public static readonly TimeSpan BaseDelay = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan MaxDelay = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Backoff before the given (1-based) attempt number: <c>Base · 2^(attempt-1)</c>, capped.
    /// Attempt 1 uses the base delay; each subsequent attempt doubles until the cap.
    /// </summary>
    public static TimeSpan BackoffFor(int attemptNumber)
    {
        if (attemptNumber <= 1)
        {
            return BaseDelay;
        }

        // Guard the shift against overflow for large attempt counts.
        var exponent = Math.Min(attemptNumber - 1, 20);
        var scaled = BaseDelay.Ticks * (1L << exponent);
        var ticks = scaled < 0 ? MaxDelay.Ticks : Math.Min(scaled, MaxDelay.Ticks);
        return TimeSpan.FromTicks(ticks);
    }
}
