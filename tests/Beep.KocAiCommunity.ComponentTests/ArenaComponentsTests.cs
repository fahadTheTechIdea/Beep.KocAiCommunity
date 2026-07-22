using Beep.KocAiCommunity.Contracts.Competitions;
using Beep.KocAiCommunity.Web.Components.Shared;
using Bunit;
using FluentAssertions;
using MudBlazor.Services;
using Xunit;

namespace Beep.KocAiCommunity.ComponentTests;

/// <summary>Base context with MudBlazor services registered for the arena components.</summary>
public abstract class ArenaTestContext : TestContext
{
    protected ArenaTestContext()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    protected static CompetitionDto Comp(Guid? id = null, string status = "active", DateTime? reveal = null) =>
        new(id ?? Guid.NewGuid(), "ESP Failure Challenge", "Predict pump failures.", status, "Company",
            reveal, HasAnswerKey: true, HasDatasets: true, "label", "id", "BinaryClassification", null,
            ParticipantCount: 7, SubmissionCount: 23, HostName: "Sara Al-Rashidi", QuotaPerDay: 5,
            MetricName: "Accuracy", HigherIsBetter: true, CreatedUtc: DateTime.UtcNow.AddDays(-3));
}

public class CountdownTimerTests : ArenaTestContext
{
    [Fact]
    public void Future_target_renders_unit_boxes()
    {
        var cut = RenderComponent<CountdownTimer>(p => p.Add(x => x.TargetUtc, DateTime.UtcNow.AddDays(3)));
        cut.Markup.Should().Contain("koc-countdown");
        cut.Markup.Should().Contain("days");
        cut.Markup.Should().NotContain("koc-countdown-urgent");   // 3 days out is not urgent
    }

    [Fact]
    public void Under_24h_is_urgent_and_past_target_reads_revealed()
    {
        var urgent = RenderComponent<CountdownTimer>(p => p.Add(x => x.TargetUtc, DateTime.UtcNow.AddHours(3)));
        urgent.Markup.Should().Contain("koc-countdown-urgent");

        var past = RenderComponent<CountdownTimer>(p => p
            .Add(x => x.TargetUtc, DateTime.UtcNow.AddMinutes(-5))
            .Add(x => x.Compact, true));
        past.Markup.Should().Contain("revealed");
    }

    [Fact]
    public void Null_target_renders_nothing()
    {
        var cut = RenderComponent<CountdownTimer>();
        cut.Markup.Trim().Should().BeEmpty();
    }
}

public class PodiumBlockTests : ArenaTestContext
{
    [Fact]
    public void Three_entries_render_staggered_columns_with_gold_center()
    {
        var entries = new List<LeaderboardEntryDto>
        {
            new(1, "u1", "Alice", 0.96),
            new(2, "u2", "Bob", 0.93),
            new(3, "u3", "Carol", 0.91),
        };

        var cut = RenderComponent<PodiumBlock>(p => p.Add(x => x.Entries, entries).Add(x => x.MyUserId, "u2"));

        cut.FindAll(".koc-podium-col").Count.Should().Be(3);
        cut.Markup.Should().Contain("koc-podium-col-1").And.Contain("koc-podium-col-2").And.Contain("koc-podium-col-3");
        cut.FindAll(".koc-podium-col")[1].ClassList.Should().Contain("koc-podium-col-1");   // gold in the middle
        cut.Markup.Should().Contain("(you)");
    }

    [Fact]
    public void Empty_board_invites_the_first_submission()
    {
        var cut = RenderComponent<PodiumBlock>(p => p.Add(x => x.Entries, new List<LeaderboardEntryDto>()));
        cut.Markup.Should().Contain("wide open");
    }
}

public class CompetitionCardTests : ArenaTestContext
{
    [Fact]
    public void Card_shows_stats_host_and_enter_link()
    {
        var comp = Comp(reveal: DateTime.UtcNow.AddDays(2));
        var cut = RenderComponent<CompetitionCard>(p => p.Add(x => x.Competition, comp));

        cut.Markup.Should().Contain($"/compete/{comp.Id}");
        cut.Markup.Should().Contain("7");                    // participants
        cut.Markup.Should().Contain("23");                   // submissions
        cut.Markup.Should().Contain("Sara Al-Rashidi");
        cut.Markup.Should().Contain("koc-deadline-bar");     // CreatedUtc + RevealUtc → progress bar
        cut.Markup.Should().Contain("koc-countdown-compact");
    }

    [Fact]
    public void Lower_is_better_metric_says_so()
    {
        var comp = Comp() with { MetricName = "RMSE", HigherIsBetter = false, TaskType = "Regression" };
        var cut = RenderComponent<CompetitionCard>(p => p.Add(x => x.Competition, comp));
        cut.Markup.Should().Contain("RMSE — lower wins");
    }
}

public class LiveBoardTests : ArenaTestContext
{
    [Fact]
    public void First_render_shows_no_movement_then_refresh_shows_arrows_and_new()
    {
        var v1 = new List<LeaderboardEntryDto> { new(1, "a", "Alice", 0.9), new(2, "b", "Bob", 0.8) };
        var cut = RenderComponent<LiveBoard>(p => p.Add(x => x.Entries, v1).Add(x => x.MyUserId, "b"));

        cut.Markup.Should().NotContain("▲").And.NotContain("▼").And.NotContain("NEW");

        // Bob overtakes Alice and Carol appears.
        var v2 = new List<LeaderboardEntryDto> { new(1, "b", "Bob", 0.95), new(2, "a", "Alice", 0.9), new(3, "c", "Carol", 0.7) };
        cut.SetParametersAndRender(p => p.Add(x => x.Entries, v2));

        cut.Markup.Should().Contain("▲1");     // Bob up one
        cut.Markup.Should().Contain("▼1");     // Alice down one
        cut.Markup.Should().Contain("NEW");    // Carol new on the board
        cut.Markup.Should().Contain("koc-row-pulse");
    }

    [Fact]
    public void Empty_board_renders_call_to_action()
    {
        var cut = RenderComponent<LiveBoard>(p => p.Add(x => x.Entries, new List<LeaderboardEntryDto>()));
        cut.Markup.Should().Contain("first on the board");
    }
}
