using Beep.KocAiCommunity.Infrastructure.Engagement;
using FluentAssertions;
using Xunit;

namespace Beep.KocAiCommunity.UnitTests;

public class BadgeRulesTests
{
    private static BadgeContext Empty => new(0, 0, 0, 3, 0, false, false, 0, 0);

    [Fact]
    public void First_barrel_unlocks_on_any_xp_event()
    {
        var ctx = Empty with { XpEventCount = 1 };
        BadgeRules.Qualifying(ctx).Should().Contain(BadgeCatalog.FirstBarrel);
    }

    [Fact]
    public void All_tracks_needs_every_published_track()
    {
        BadgeRules.Qualifying(Empty with { TrackCompletionCount = 2, PublishedTrackCount = 3 })
            .Should().NotContain(BadgeCatalog.AllTracks);
        BadgeRules.Qualifying(Empty with { TrackCompletionCount = 3, PublishedTrackCount = 3 })
            .Should().Contain(BadgeCatalog.AllTracks);
    }

    [Fact]
    public void Streak_badges_unlock_at_their_thresholds()
    {
        BadgeRules.Qualifying(Empty with { CurrentStreakDays = 6 }).Should().NotContain(BadgeCatalog.Streak7);
        BadgeRules.Qualifying(Empty with { CurrentStreakDays = 7 }).Should().Contain(BadgeCatalog.Streak7);
        BadgeRules.Qualifying(Empty with { CurrentStreakDays = 30 }).Should().Contain(new[] { BadgeCatalog.Streak7, BadgeCatalog.Streak30 });
    }

    [Fact]
    public void Winner_and_podium_are_independent_of_points()
    {
        BadgeRules.Qualifying(Empty with { HasCompetitionWin = true, HasCompetitionPodium = true })
            .Should().Contain(new[] { BadgeCatalog.CompetitionWinner, BadgeCatalog.Podium });
    }

    [Fact]
    public void NewlyEarned_excludes_already_earned()
    {
        var ctx = Empty with { XpEventCount = 1, ScoredSubmissionCount = 1 };
        var already = new HashSet<string> { BadgeCatalog.FirstBarrel };

        var newly = BadgeRules.NewlyEarned(ctx, already);

        newly.Should().Contain(BadgeCatalog.FirstSubmission);
        newly.Should().NotContain(BadgeCatalog.FirstBarrel);
    }
}
