using Beep.KocAiCommunity.Client;
using Beep.KocAiCommunity.Contracts.Competitions;
using Beep.KocAiCommunity.Contracts.Engagement;
using Beep.KocAiCommunity.Desktop.Local;
using Beep.KocAiCommunity.ServiceDefaults.Security;
using Beep.KocAiCommunity.Web.Components.Pages;
using Bunit;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using Xunit;

namespace Beep.KocAiCommunity.ComponentTests;

/// <summary>
/// The area filter on the top bar. A production engineer should not have to scroll past payroll
/// challenges to find theirs, so the home page narrows to one KOC domain — and says so plainly, because
/// a filter you cannot see gets reported as "the competitions have disappeared".
/// </summary>
public class HomeCategoryFilterTests : TestContext
{
    private sealed class FakeApi(IReadOnlyList<CompetitionDto> competitions) : RemoteFallbackKocApiClient(null!)
    {
        public override Task<IReadOnlyList<CompetitionDto>> GetCompetitionsAsync(CancellationToken ct = default) =>
            Task.FromResult(competitions);

        public override Task<IReadOnlyList<CompetitionCategoryDto>> GetCompetitionCategoriesAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<CompetitionCategoryDto>>(
            [
                new("production", "Production", "Rates and decline.", "OilBarrel", true, 0, 1),
                new("people", "People & HR", "Payroll and resourcing.", "Groups", true, 1, 1),
                new("medical", "Medical & Health", "Ahmadi Hospital.", "MedicalServices", true, 2, 0),
            ]);

        public override Task<IReadOnlyList<LeaderboardEntryDto>> GetLeaderboardAsync(Guid competitionId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<LeaderboardEntryDto>>([]);

        public override Task<IReadOnlyList<XpLeaderboardRowDto>> GetXpLeaderboardAsync(string period, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<XpLeaderboardRowDto>>([]);

        public override Task<IReadOnlyList<TeamLeaderboardRowDto>> GetTeamLeaderboardAsync(string period, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<TeamLeaderboardRowDto>>([]);
    }

    private static CompetitionDto Competition(string title, string categoryCode, string categoryName) =>
        new(Guid.NewGuid(), title, "Predict something.", "active",
            "Company", DateTime.UtcNow.AddDays(4), false, true, "label", "id", "BinaryClassification", null,
            ParticipantCount: 12, SubmissionCount: 40, HostName: "Sara", QuotaPerDay: 5,
            MetricName: "Accuracy", HigherIsBetter: true, CreatedUtc: DateTime.UtcNow.AddDays(-2),
            CategoryCode: categoryCode, CategoryName: categoryName);

    private IRenderedComponent<Home> Render(string? category, params CompetitionDto[] comps)
    {
        Services.AddMudServices();
        Services.AddLogging();
        Services.AddLocalization();
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<IKocApiClient>(new FakeApi(comps));
        Services.AddSingleton(new DevIdentity());
        Services.AddSingleton(new KocSetupStore(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Auth:SignInWith"] = "SiteAccounts" })
            .Build()));

        // The filter is read from the address, not passed in — bUnit enforces that, which is itself
        // proof the parameter is bound to the query string rather than to a caller.
        if (category is not null)
        {
            Services.GetRequiredService<NavigationManager>().NavigateTo($"/?category={category}");
        }

        return RenderComponent<Home>();
    }

    private static readonly CompetitionDto[] Sample =
    [
        Competition("Daily Oil Rate", "production", "Production"),
        Competition("Payroll Integrity", "people", "People & HR"),
    ];

    [Fact]
    public void No_area_chosen_shows_every_competition_and_no_filter_banner()
    {
        var cut = Render(category: null, Sample);

        cut.Markup.Should().Contain("Daily Oil Rate");
        cut.Markup.Should().Contain("Payroll Integrity");
        cut.Markup.Should().NotContain("koc-filterbar", "nothing is filtered, so there is nothing to announce");
        cut.Markup.Should().Contain("<b>2</b>", "the headline figure counts both running competitions");
    }

    [Fact]
    public void Choosing_an_area_hides_the_other_areas_and_says_which_one_is_in_force()
    {
        var cut = Render(category: "people", Sample);

        cut.Markup.Should().Contain("Payroll Integrity");
        cut.Markup.Should().NotContain("Daily Oil Rate");

        // Named, and clearable in one click — otherwise this looks like data loss.
        cut.Markup.Should().Contain("koc-filterbar");
        cut.Markup.Should().Contain("People &amp; HR");
        cut.Markup.Should().Contain("Show all areas");

        // The figures describe the filtered page, not the whole platform — a strip claiming two
        // competitions above a list showing one reads as a bug.
        cut.Markup.Should().Contain("<b>1</b>");
        cut.Markup.Should().NotContain("<b>2</b>");
    }

    [Fact]
    public void The_worked_example_in_the_hero_follows_the_chosen_area()
    {
        var cut = Render(category: "people", Sample);

        // The example is what explains machine learning to somebody who has never met it, so it has to
        // come from a world they know. Showing HR a vibration trace explains nothing.
        cut.Markup.Should().Contain("payroll run");
        cut.Markup.Should().Contain("base pay");
        cut.Markup.Should().NotContain("Pump P-114");
        cut.Markup.Should().NotContain("vibration");
    }

    [Fact]
    public void With_no_area_chosen_the_hero_shows_the_equipment_example()
    {
        var cut = Render(category: null, Sample);

        cut.Markup.Should().Contain("Pump P-114");
        cut.Markup.Should().Contain("vibration");
    }

    [Fact]
    public void An_area_with_nothing_running_is_named_and_offers_the_way_out()
    {
        // The case the banner matters most for: the category exists, but no competition uses it yet, so
        // the name has to come from the category list rather than from a competition row.
        var cut = Render(category: "medical", Sample);

        cut.Markup.Should().Contain("koc-emptyarea");
        cut.Markup.Should().Contain("Medical &amp; Health");
        cut.Markup.Should().NotContain("medical\" just yet", "the code is never shown when the name is known");
        cut.Markup.Should().Contain("Show all areas");
    }
}
