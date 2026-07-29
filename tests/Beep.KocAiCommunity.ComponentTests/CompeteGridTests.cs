using Beep.KocAiCommunity.Client;
using Beep.KocAiCommunity.Contracts.Competitions;
using Beep.KocAiCommunity.Contracts.Identity;
using Beep.KocAiCommunity.Desktop.Local;
using Beep.KocAiCommunity.Web.Components.Pages;
using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using Xunit;

namespace Beep.KocAiCommunity.ComponentTests;

/// <summary>The /compete browse grid: cards from the enriched DTO, filters, spotlight podium.</summary>
public class CompeteGridTests : TestContext
{
    /// <summary>Fake API: overrides only what the grid calls; everything else would hit the null remote.</summary>
    private sealed class FakeApi(IReadOnlyList<CompetitionDto> competitions) : RemoteFallbackKocApiClient(null!)
    {
        public override Task<MeResponse?> GetMeAsync(CancellationToken ct = default) =>
            Task.FromResult<MeResponse?>(new MeResponse("u1", "Test", ["Employee"], "Employee", null, null, "Company"));

        public override Task<IReadOnlyList<CompetitionDto>> GetCompetitionsAsync(CancellationToken ct = default) =>
            Task.FromResult(competitions);

        public override Task<IReadOnlyList<LeaderboardEntryDto>> GetLeaderboardAsync(Guid competitionId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<LeaderboardEntryDto>>([new(1, "a", "Alice", 0.97)]);
    }

    private static CompetitionDto Comp(string title, string status, string task = "BinaryClassification") =>
        new(Guid.NewGuid(), title, "desc", status, "Company", null, true, true, "label", "id", task, null,
            ParticipantCount: 3, SubmissionCount: 9, HostName: "Host", QuotaPerDay: 5,
            MetricName: "Accuracy", HigherIsBetter: true, CreatedUtc: DateTime.UtcNow.AddDays(-1));

    private IRenderedComponent<Compete> Render(params CompetitionDto[] comps)
    {
        Services.AddMudServices();
        // Components take IStringLocalizer now; without the resource machinery registered
        // every render fails on DI rather than on anything the test is about.
        Services.AddLogging();
        Services.AddLocalization();
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<IKocApiClient>(new FakeApi(comps));
        Services.AddSingleton(new DevIdentity());
        return RenderComponent<Compete>();
    }

    [Fact]
    public void Grid_renders_a_card_per_competition_with_spotlight_podium()
    {
        var cut = Render(Comp("Alpha", "active"), Comp("Beta", "active"), Comp("Gamma", "concluded"));

        cut.Markup.Should().Contain("Alpha").And.Contain("Beta").And.Contain("Gamma");
        cut.FindAll(".koc-blueprint").Count.Should().BeGreaterThanOrEqualTo(3);
        cut.Markup.Should().Contain("koc-podium-col");          // spotlight card carries the fetched podium
        cut.Markup.Should().Contain("Host a competition");      // MaxCompetitionScope set → host button
    }

    [Fact]
    public void Status_filter_narrows_the_grid()
    {
        var cut = Render(Comp("Alpha", "active"), Comp("Gamma", "concluded"));

        // The chip reads "Concluded", not the stored code — status codes are lowercase on the wire and
        // words on the screen, so they can be translated like every other label.
        cut.FindAll(".mud-chip").First(c => c.TextContent.Trim() == "Concluded").Click();
        cut.Markup.Should().Contain("Gamma").And.NotContain("Alpha");
    }

    [Fact]
    public void Empty_state_invites_hosting()
    {
        var cut = Render();
        cut.Markup.Should().Contain("No competitions yet");
    }
}
