using Beep.KocAiCommunity.Client;
using Beep.KocAiCommunity.Contracts.Competitions;
using Beep.KocAiCommunity.Desktop.Local;
using Beep.KocAiCommunity.Web.Components.Pages;
using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using Xunit;

namespace Beep.KocAiCommunity.ComponentTests;

/// <summary>The landing page leads with the featured-competition arena hero.</summary>
public class HomeHeroTests : TestContext
{
    private sealed class FakeApi(IReadOnlyList<CompetitionDto> competitions) : RemoteFallbackKocApiClient(null!)
    {
        public override Task<IReadOnlyList<CompetitionDto>> GetCompetitionsAsync(CancellationToken ct = default) =>
            Task.FromResult(competitions);

        public override Task<IReadOnlyList<LeaderboardEntryDto>> GetLeaderboardAsync(Guid competitionId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<LeaderboardEntryDto>>([new(1, "a", "Alice", 0.97), new(2, "b", "Bob", 0.9)]);
    }

    private IRenderedComponent<Home> Render(params CompetitionDto[] comps)
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<IKocApiClient>(new FakeApi(comps));
        Services.AddSingleton(new DevIdentity());   // defaults to a signed-in persona
        return RenderComponent<Home>();
    }

    [Fact]
    public void Active_competition_renders_the_arena_hero_with_enter_link()
    {
        var comp = new CompetitionDto(Guid.NewGuid(), "ESP Failure Challenge", "Predict failures.", "active",
            "Company", DateTime.UtcNow.AddDays(4), true, true, "label", "id", "BinaryClassification", null,
            ParticipantCount: 12, SubmissionCount: 40, HostName: "Sara", QuotaPerDay: 5,
            MetricName: "Accuracy", HigherIsBetter: true, CreatedUtc: DateTime.UtcNow.AddDays(-2));

        var cut = Render(comp);

        cut.Markup.Should().Contain("koc-arena-banner");
        cut.Markup.Should().Contain("Featured competition");
        cut.Markup.Should().Contain("ESP Failure Challenge");
        cut.Markup.Should().Contain($"/compete/{comp.Id}");     // ENTER deep link
        cut.Markup.Should().Contain("koc-countdown");           // live countdown
        cut.Markup.Should().Contain("koc-podium-col");          // live podium
    }

    [Fact]
    public void No_active_competition_means_no_hero_but_page_still_renders()
    {
        var cut = Render();
        cut.Markup.Should().NotContain("koc-arena-banner");
        cut.Markup.Should().Contain("champions");               // the rest of the page is intact
    }
}
