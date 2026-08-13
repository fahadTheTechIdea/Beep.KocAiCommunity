using Beep.KocAiCommunity.Client;
using Beep.KocAiCommunity.Contracts.Competitions;
using Beep.KocAiCommunity.Contracts.Engagement;
using Beep.KocAiCommunity.Desktop.Local;
using Beep.KocAiCommunity.ServiceDefaults.Security;
using Beep.KocAiCommunity.Web.Components.Pages;
using Bunit;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using Xunit;

namespace Beep.KocAiCommunity.ComponentTests;

/// <summary>
/// The landing page leads with what the platform is, then proves it with live competition and
/// leaderboard data — and asks the visitor to sign in exactly once.
/// </summary>
public class HomeHeroTests : TestContext
{
    private sealed class FakeApi(IReadOnlyList<CompetitionDto> competitions) : RemoteFallbackKocApiClient(null!)
    {
        public override Task<IReadOnlyList<CompetitionDto>> GetCompetitionsAsync(CancellationToken ct = default) =>
            Task.FromResult(competitions);

        public override Task<IReadOnlyList<LeaderboardEntryDto>> GetLeaderboardAsync(Guid competitionId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<LeaderboardEntryDto>>(
                [.. Enumerable.Range(1, 12).Select(i => new LeaderboardEntryDto(i, $"u{i}", $"Competitor {i}", 1.0 - (i * 0.01)))]);

        public override Task<IReadOnlyList<XpLeaderboardRowDto>> GetXpLeaderboardAsync(string period, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<XpLeaderboardRowDto>>(
                [.. Enumerable.Range(1, 12).Select(i => new XpLeaderboardRowDto(i, $"u{i}", $"Learner {i}", "185-worker.png", 3, "Driller", 500 - i, false))]);

        public override Task<IReadOnlyList<TeamLeaderboardRowDto>> GetTeamLeaderboardAsync(string period, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<TeamLeaderboardRowDto>>(
                [.. Enumerable.Range(1, 4).Select(i => new TeamLeaderboardRowDto(i, Guid.NewGuid(), $"Team {i}", 8, 900 - i, 112.5, false))]);
    }

    private static CompetitionDto Competition(string title = "ESP Failure Challenge") =>
        new(Guid.NewGuid(), title, "Predict failures.", "active",
            "Company", DateTime.UtcNow.AddDays(4), true, true, "label", "id", "BinaryClassification", null,
            ParticipantCount: 12, SubmissionCount: 40, HostName: "Sara", QuotaPerDay: 5,
            MetricName: "Accuracy", HigherIsBetter: true, CreatedUtc: DateTime.UtcNow.AddDays(-2));

    private IRenderedComponent<Home> Render(bool asGuest, params CompetitionDto[] comps)
    {
        Services.AddMudServices();
        // Components take IStringLocalizer now; without the resource machinery registered
        // every render fails on DI rather than on anything the test is about.
        Services.AddLogging();
        Services.AddLocalization();
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<IKocApiClient>(new FakeApi(comps));

        var identity = new DevIdentity();
        if (asGuest)
        {
            identity.SetPersona("guest");
        }

        Services.AddSingleton(identity);

        // Site accounts, so the page offers its own login page rather than a persona picker.
        Services.AddSingleton(new KocSetupStore(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Auth:SignInWith"] = "SiteAccounts" })
            .Build()));

        return RenderComponent<Home>();
    }

    [Fact]
    public void The_featured_competition_shows_its_full_standings_not_a_three_row_podium()
    {
        var comp = Competition();

        var cut = Render(asGuest: false, comp);

        cut.Markup.Should().Contain("koc-arena-banner");
        cut.Markup.Should().Contain("Featured competition");
        cut.Markup.Should().Contain("ESP Failure Challenge");
        cut.Markup.Should().Contain($"/compete/{comp.Id}");     // ENTER deep link
        cut.Markup.Should().Contain("koc-countdown");           // live countdown

        // The board is the centrepiece: the full LiveBoard, deep enough to read as a real table.
        cut.Markup.Should().Contain("Live standings");
        cut.Markup.Should().Contain("Competitor 10", "the board runs well past a top-three podium");
    }

    [Fact]
    public void A_guest_is_asked_to_sign_in_exactly_once()
    {
        var cut = Render(asGuest: true, Competition());

        // Four sign-in buttons competed for attention before; the ask belongs in the hero alone.
        //
        // Since the newcomer revamp the ask is a link beside the primary button rather than the button
        // itself: someone who has never heard of machine learning cannot decide whether to sign in
        // before they know what this place is, so the primary action offers them the first lesson and
        // the ask sits next to it. /learn is browsable by guests, so that is a real door, not a gate.
        // Counted across both element kinds — "exactly once" is the rule the page has to keep; being a
        // button was only ever how it happened to be kept.
        var asks = cut.FindComponents<MudButton>().Select(b => b.Markup)
            .Concat(cut.FindComponents<MudLink>().Select(l => l.Markup))
            .Count(m => m.Contains("Sign in", StringComparison.OrdinalIgnoreCase));

        asks.Should().Be(1, "the page asks once, in the hero");

        // …and what the guest is offered first is the way in, not the gate.
        cut.Markup.Should().Contain("Start from zero", "a newcomer's first step is a lesson");
    }

    [Fact]
    public void A_signed_in_member_is_never_asked_to_sign_in()
    {
        var cut = Render(asGuest: false, Competition());

        cut.Markup.Should().NotContain("Sign in");
        cut.Markup.Should().Contain("Continue where you left off");
    }

    [Fact]
    public void The_guest_hero_asks_with_a_real_button_and_offers_a_no_account_door()
    {
        var cut = Render(asGuest: true, Competition());

        // The one ask is now the hero's filled button rather than a text link — the page exists to get
        // a colleague to join. The count rule above still holds; this pins the promotion.
        cut.FindComponents<MudButton>().Select(b => b.Markup)
            .Should().Contain(m => m.Contains("Sign in", StringComparison.OrdinalIgnoreCase));

        // And beside it, the door for whoever is not ready: try the first lesson without an account.
        cut.Markup.Should().Contain("Try it first — no account needed");
    }

    [Fact]
    public void A_newcomer_is_shown_the_badge_cabinet_with_the_catalogs_real_medals()
    {
        var cut = Render(asGuest: true, Competition());

        // The what-you-win section: Barrels, the rank ladder, and six real badges. Names must match
        // BadgeCatalog — the page must never advertise a medal that does not exist.
        cut.Markup.Should().Contain("koc-badgewall");
        cut.Markup.Should().Contain("Wildcatter");
        cut.Markup.Should().Contain("Gusher");
        cut.Markup.Should().Contain("Steady Pump");
        cut.Markup.Should().Contain("Chief Geoscientist", "the rank ladder shows where the climb ends");
    }

    [Fact]
    public void The_champions_board_renders_at_full_size()
    {
        var cut = Render(asGuest: false, Competition());

        cut.Markup.Should().Contain("This month's champions");
        cut.Markup.Should().Contain("koc-board-row");
        cut.Markup.Should().Contain("Learner 10", "ten rows make it a standings table, not a teaser");
    }

    [Fact]
    public void No_active_competition_means_no_arena_but_the_page_still_stands()
    {
        var cut = Render(asGuest: false);

        cut.Markup.Should().NotContain("koc-arena-banner");
        cut.Markup.Should().Contain("champions");               // the rest of the page is intact
    }
}
