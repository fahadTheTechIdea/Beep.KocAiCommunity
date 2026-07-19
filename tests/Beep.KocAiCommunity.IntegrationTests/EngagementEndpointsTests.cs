using System.Net;
using System.Net.Http.Json;
using Beep.KocAiCommunity.Contracts.Engagement;
using FluentAssertions;
using Xunit;

namespace Beep.KocAiCommunity.IntegrationTests;

public class EngagementEndpointsTests(KocApiFactory factory) : IClassFixture<KocApiFactory>
{
    private readonly KocApiFactory _factory = factory;

    [Fact]
    public async Task My_profile_is_created_lazily_and_returned()
    {
        var emp = _factory.CreateClientAs("emp1", "Employee");

        var profile = await emp.GetFromJsonAsync<ProfileDto>("/api/v1/profiles/me");

        profile.Should().NotBeNull();
        profile!.UserId.Should().Be("emp1");
        profile.Level.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task Badge_catalog_and_avatars_are_available()
    {
        var emp = _factory.CreateClientAs("emp1", "Employee");

        var badges = await emp.GetFromJsonAsync<List<BadgeDto>>("/api/v1/engagement/badges/catalog");
        var avatars = await emp.GetFromJsonAsync<List<string>>("/api/v1/engagement/avatars");

        badges.Should().NotBeNullOrEmpty();
        badges!.Should().Contain(b => b.Code == "first-barrel");
        avatars.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Kudos_round_trips_and_pays_the_recipient()
    {
        var giver = _factory.CreateClientAs("mgr1", "Manager");
        var receiver = _factory.CreateClientAs("emp2", "Employee");

        var before = await receiver.GetFromJsonAsync<ProfileDto>("/api/v1/profiles/me");

        var response = await giver.PostAsJsonAsync("/api/v1/engagement/kudos",
            new GiveKudosRequest("emp2", "excellent submission!", "🌟", null, null));
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var after = await receiver.GetFromJsonAsync<ProfileDto>("/api/v1/profiles/me");
        after!.XpTotal.Should().Be(before!.XpTotal + 15);

        var kudos = await receiver.GetFromJsonAsync<List<KudosDto>>("/api/v1/engagement/kudos/emp2");
        kudos!.Should().Contain(k => k.FromUserId == "mgr1" && k.Message == "excellent submission!");
    }

    [Fact]
    public async Task Leaderboards_require_authentication()
    {
        var anon = _factory.CreateClientAs(null);

        (await anon.GetAsync("/api/v1/engagement/leaderboard")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anon.GetAsync("/api/v1/profiles/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Leaderboards_return_ok_for_an_employee()
    {
        var emp = _factory.CreateClientAs("emp1", "Employee");

        (await emp.GetAsync("/api/v1/engagement/leaderboard?period=all")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await emp.GetAsync("/api/v1/engagement/teams?period=week")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await emp.GetAsync("/api/v1/engagement/activity")).StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
