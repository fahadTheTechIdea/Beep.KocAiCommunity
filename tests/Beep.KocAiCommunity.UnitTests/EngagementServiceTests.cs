using Beep.KocAiCommunity.Application.Engagement;
using Beep.KocAiCommunity.Contracts.Engagement;
using Beep.KocAiCommunity.Infrastructure.Authorization;
using Beep.KocAiCommunity.Infrastructure.Engagement;
using Beep.KocAiCommunity.Infrastructure.Messaging;
using Beep.KocAiCommunity.Infrastructure.Organization;
using FluentAssertions;
using Xunit;

namespace Beep.KocAiCommunity.UnitTests;

public class EngagementServiceTests
{
    private static EngagementService Build(OrgTestContext ctx) =>
        new(ctx.Db, new OutboxWriter(ctx.Db), new VisibilityEvaluator(ctx.Db), new OrgDirectory(ctx.Db));

    [Fact]
    public async Task Completing_a_lesson_awards_barrels_and_the_first_badge()
    {
        using var ctx = new OrgTestContext();
        var svc = Build(ctx);

        await svc.AwardXpAsync("emp1", XpSources.LessonCompleted, "lesson", Guid.NewGuid());

        var profile = await svc.GetProfileAsync("emp1");
        profile.XpTotal.Should().Be(25);
        profile.CurrentStreakDays.Should().Be(1);
        profile.Badges.Select(b => b.Code).Should().Contain(BadgeCatalog.FirstBarrel);
    }

    [Fact]
    public async Task Awarding_the_same_ref_twice_is_idempotent()
    {
        using var ctx = new OrgTestContext();
        var svc = Build(ctx);
        var lessonId = Guid.NewGuid();

        await svc.AwardXpAsync("emp1", XpSources.LessonCompleted, "lesson", lessonId);
        await svc.AwardXpAsync("emp1", XpSources.LessonCompleted, "lesson", lessonId);

        (await svc.GetProfileAsync("emp1")).XpTotal.Should().Be(25);
    }

    [Fact]
    public async Task First_scored_submission_pays_a_bonus_and_the_wildcatter_badge()
    {
        using var ctx = new OrgTestContext();
        var svc = Build(ctx);

        await svc.AwardXpAsync("emp1", XpSources.SubmissionScored, "submission", Guid.NewGuid());

        var profile = await svc.GetProfileAsync("emp1");
        profile.XpTotal.Should().Be(70); // 20 scored + 50 first-submission bonus
        profile.Badges.Select(b => b.Code).Should().Contain(BadgeCatalog.FirstSubmission);
    }

    [Fact]
    public async Task Team_leaderboard_ranks_units_by_average_barrels()
    {
        using var ctx = new OrgTestContext();
        var svc = Build(ctx);

        await svc.AwardXpAsync("emp1", XpSources.TrackCompleted, "track", Guid.NewGuid()); // 150, team T1
        await svc.AwardXpAsync("emp2", XpSources.LessonCompleted, "lesson", Guid.NewGuid()); // 25, team T2

        var teams = await svc.GetTeamLeaderboardAsync("emp1", LeaderboardPeriod.AllTime);

        teams.Should().HaveCount(2);
        teams[0].OrgUnitId.Should().Be(ctx.T1);     // higher average leads
        teams.Single(t => t.OrgUnitId == ctx.T1).IsMyTeam.Should().BeTrue();
    }

    [Fact]
    public async Task Xp_leaderboard_always_includes_the_caller()
    {
        using var ctx = new OrgTestContext();
        var svc = Build(ctx);
        await svc.AwardXpAsync("emp1", XpSources.LessonCompleted, "lesson", Guid.NewGuid());

        var board = await svc.GetXpLeaderboardAsync("emp1", LeaderboardPeriod.AllTime);

        board.Should().Contain(r => r.IsMe && r.UserId == "emp1");
    }

    [Fact]
    public async Task Kudos_pays_the_recipient_and_is_recorded()
    {
        using var ctx = new OrgTestContext();
        var svc = Build(ctx);

        await svc.GiveKudosAsync("emp1", new GiveKudosRequest("emp2", "great model!", "🚀", null, null));

        (await svc.GetProfileAsync("emp2")).XpTotal.Should().Be(15);
        var kudos = await svc.GetKudosForAsync("emp2");
        kudos.Should().ContainSingle(k => k.FromUserId == "emp1" && k.Emoji == "🚀");
    }

    [Fact]
    public async Task Kudos_to_self_is_rejected()
    {
        using var ctx = new OrgTestContext();
        var svc = Build(ctx);

        await FluentActions
            .Awaiting(() => svc.GiveKudosAsync("emp1", new GiveKudosRequest("emp1", "me!", "👏", null, null)))
            .Should().ThrowAsync<EngagementException>();
    }

    [Fact]
    public async Task Kudos_are_capped_at_ten_per_day()
    {
        using var ctx = new OrgTestContext();
        var svc = Build(ctx);

        for (var i = 0; i < 10; i++)
        {
            await svc.GiveKudosAsync("emp1", new GiveKudosRequest("emp2", $"thanks {i}", "👏", null, null));
        }

        await FluentActions
            .Awaiting(() => svc.GiveKudosAsync("emp1", new GiveKudosRequest("emp2", "one too many", "👏", null, null)))
            .Should().ThrowAsync<EngagementException>();
    }
}
