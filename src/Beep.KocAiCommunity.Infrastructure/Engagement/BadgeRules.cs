namespace Beep.KocAiCommunity.Infrastructure.Engagement;

/// <summary>
/// A snapshot of a user's earned state, gathered from the XP ledger + profile once per award.
/// Pure inputs so the rule evaluation itself is deterministic and unit-testable.
/// </summary>
public sealed record BadgeContext(
    int XpEventCount,
    int ScoredSubmissionCount,
    int TrackCompletionCount,
    int PublishedTrackCount,
    int DiscussionCreatedCount,
    bool HasCompetitionWin,
    bool HasCompetitionPodium,
    int CurrentStreakDays,
    int KudosReceivedCount);

/// <summary>Evaluates the badge catalog against a <see cref="BadgeContext"/>. No side effects.</summary>
public static class BadgeRules
{
    /// <summary>All badge codes the context currently qualifies for (earned or not — caller diffs).</summary>
    public static IEnumerable<string> Qualifying(BadgeContext c)
    {
        if (c.XpEventCount >= 1) yield return BadgeCatalog.FirstBarrel;
        if (c.ScoredSubmissionCount >= 1) yield return BadgeCatalog.FirstSubmission;
        if (c.HasCompetitionWin) yield return BadgeCatalog.CompetitionWinner;
        if (c.HasCompetitionPodium) yield return BadgeCatalog.Podium;
        if (c.TrackCompletionCount >= 1) yield return BadgeCatalog.FirstTrack;
        if (c.PublishedTrackCount > 0 && c.TrackCompletionCount >= c.PublishedTrackCount) yield return BadgeCatalog.AllTracks;
        if (c.CurrentStreakDays >= 7) yield return BadgeCatalog.Streak7;
        if (c.CurrentStreakDays >= 30) yield return BadgeCatalog.Streak30;
        if (c.KudosReceivedCount >= 10) yield return BadgeCatalog.Helper10;
        if (c.DiscussionCreatedCount >= 5) yield return BadgeCatalog.DiscussionStarter;
    }

    /// <summary>Codes newly earned this evaluation (qualifying minus already-earned).</summary>
    public static IReadOnlyList<string> NewlyEarned(BadgeContext c, IReadOnlySet<string> alreadyEarned) =>
        [.. Qualifying(c).Where(code => !alreadyEarned.Contains(code))];
}
