using System.Net;
using System.Net.Http.Json;
using Beep.KocAiCommunity.Contracts.Competitions;
using FluentAssertions;
using Xunit;

namespace Beep.KocAiCommunity.IntegrationTests;

/// <summary>
/// What a signed-out visitor may see of the arena.
/// <para>
/// Browsing competitions and reading leaderboards is open, the way Learn and Community are — seeing what
/// people are competing on is what makes somebody want an account, and it was behind the sign-in wall.
/// Opening it moves a leak rule to the front: a competition scoped to one team must stay invisible to an
/// anonymous caller <b>even when they hold its id</b>, and every action must still require an account.
/// Those two are what these tests are actually for; the happy path is the easy half.
/// </para>
/// </summary>
public class PublicBrowsingTests(KocApiFactory factory) : IClassFixture<KocApiFactory>
{
    private readonly KocApiFactory _factory = factory;

    private async Task<Guid> CreateAsync(string scope, string title)
    {
        var admin = _factory.CreateClientAs($"pub-{scope}", competitionCreator: false, "Employee", "PlatformAdmin");
        var response = await admin.PostAsJsonAsync("/api/v1/competitions",
            new CreateCompetitionRequest(title, "for the public-browsing tests", scope, null, null, 5, "accuracy"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<CompetitionDto>())!.Id;
    }

    [Fact]
    public async Task A_visitor_can_browse_the_arena_without_signing_in()
    {
        var companyId = await CreateAsync("Company", "Public browse — company wide");
        var guest = _factory.CreateClientAs(sub: null);

        var response = await guest.GetAsync("/api/v1/competitions");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<List<CompetitionDto>>())!
            .Should().Contain(c => c.Id == companyId);
    }

    [Fact]
    public async Task Browsing_signed_out_never_shows_a_team_private_competition()
    {
        var teamId = await CreateAsync("Team", "Public browse — team only");
        var guest = _factory.CreateClientAs(sub: null);

        var listed = await guest.GetFromJsonAsync<List<CompetitionDto>>("/api/v1/competitions");

        listed!.Should().NotContain(c => c.Id == teamId, "a team-scoped competition is not public");
    }

    [Fact]
    public async Task Holding_the_id_of_a_private_competition_is_not_enough_to_open_it()
    {
        // The leak that matters. Not 403 — telling an anonymous caller that a private competition exists
        // is itself the disclosure. To them it simply is not there.
        var teamId = await CreateAsync("Team", "Public browse — direct link");
        var guest = _factory.CreateClientAs(sub: null);

        (await guest.GetAsync($"/api/v1/competitions/{teamId}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await guest.GetAsync($"/api/v1/competitions/{teamId}/leaderboard"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task A_company_wide_competition_and_its_board_are_readable_signed_out()
    {
        var companyId = await CreateAsync("Company", "Public browse — open board");
        var guest = _factory.CreateClientAs(sub: null);

        (await guest.GetAsync($"/api/v1/competitions/{companyId}")).StatusCode.Should().Be(HttpStatusCode.OK);

        var board = await guest.GetAsync($"/api/v1/competitions/{companyId}/leaderboard");
        board.StatusCode.Should().Be(HttpStatusCode.OK);
        (await board.Content.ReadFromJsonAsync<List<LeaderboardEntryDto>>()).Should().NotBeNull();
    }

    [Fact]
    public async Task Opening_the_shop_window_did_not_open_the_shop()
    {
        // Everything that acts — takes the data, spends quota, puts a name on the board — still needs an
        // account. If this ever passes for a guest, the change went too far.
        var companyId = await CreateAsync("Company", "Public browse — actions stay closed");
        var guest = _factory.CreateClientAs(sub: null);

        (await guest.GetAsync($"/api/v1/competitions/{companyId}/data/train"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await guest.GetAsync($"/api/v1/competitions/{companyId}/submissions"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await guest.PostAsJsonAsync("/api/v1/competitions",
            new CreateCompetitionRequest("Nope", "y", "Company", null, null, 5, "accuracy")))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
