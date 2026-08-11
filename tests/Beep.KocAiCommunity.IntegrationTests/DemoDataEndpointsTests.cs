using System.Net;
using System.Net.Http.Json;
using Beep.KocAiCommunity.Contracts.Admin;
using Beep.KocAiCommunity.Contracts.Community;
using Beep.KocAiCommunity.Contracts.Competitions;
using FluentAssertions;
using Xunit;

namespace Beep.KocAiCommunity.IntegrationTests;

/// <summary>Admin demo data: seed a full explorable demo, then remove it precisely.</summary>
public class DemoDataEndpointsTests(KocApiFactory factory) : IClassFixture<KocApiFactory>
{
    private readonly KocApiFactory _factory = factory;

    private HttpClient Admin() => _factory.CreateClientAs("demo-admin-user", "Employee", "PlatformAdmin");

    [Fact]
    public async Task Demo_endpoints_require_platform_admin()
    {
        var employee = _factory.CreateClientAs("plain-emp2", "Employee");
        (await employee.GetAsync("/api/v1/admin/demo")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await employee.PostAsync("/api/v1/admin/demo/seed", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Seed_creates_explorable_content_and_unseed_removes_it()
    {
        var admin = Admin();

        // Start clean (other tests share this fixture's database).
        await admin.PostAsync("/api/v1/admin/demo/unseed", null);

        // The competitions are the platform's own — they are here before any demo data is.
        var competitions = await admin.GetFromJsonAsync<List<CompetitionDto>>("/api/v1/competitions");
        competitions.Should().Contain(c => c.Status == "active" && c.HasDatasets);
        var competitionCount = competitions!.Count;

        var seeded = await (await admin.PostAsync("/api/v1/admin/demo/seed", null)).Content.ReadFromJsonAsync<DemoDataStatusDto>();
        seeded!.Seeded.Should().BeTrue();
        seeded.Users.Should().BeGreaterThan(0);
        seeded.Submissions.Should().BeGreaterThan(0);

        // Seeding people does not invent challenges for them to enter.
        (await admin.GetFromJsonAsync<List<CompetitionDto>>("/api/v1/competitions"))!
            .Count.Should().Be(competitionCount);

        // The demo content is actually visible through the normal, company-scoped surfaces.
        var discussions = await admin.GetFromJsonAsync<List<DiscussionDto>>("/api/v1/discussions");
        discussions!.Count(d => d.Title.StartsWith("[Demo]")).Should().BeGreaterThanOrEqualTo(3);

        // One of the platform's competitions now has a populated leaderboard including the dev
        // personas. Which one the demo picks is its own business, so find the board rather than
        // assume an order the competition list does not promise.
        var boards = new List<(CompetitionDto Competition, List<LeaderboardEntryDto> Entries)>();
        foreach (var competition in competitions.Where(c => c.Status == "active"))
        {
            boards.Add((competition,
                (await admin.GetFromJsonAsync<List<LeaderboardEntryDto>>($"/api/v1/competitions/{competition.Id}/leaderboard"))!));
        }

        var (active, leaderboard) = boards.OrderByDescending(b => b.Entries.Count).First();
        leaderboard.Count.Should().BeGreaterThanOrEqualTo(10);
        leaderboard.Should().Contain(e => e.UserId == "dev-admin");

        // Datasets are trainable (they carry a real file), and the demo experiment exists for dev-admin.
        var datasets = await admin.GetFromJsonAsync<List<Beep.KocAiCommunity.Contracts.Datasets.DatasetDto>>("/api/v1/datasets");
        datasets.Should().Contain(d => d.Name.StartsWith("[Demo]") && d.HasFile);
        var devAdmin = _factory.CreateClientAs("dev-admin", "Employee", "PlatformAdmin");
        var experiments = await devAdmin.GetFromJsonAsync<List<Beep.KocAiCommunity.Contracts.Experiments.ExperimentDto>>("/api/v1/experiments");
        experiments.Should().Contain(e => e.Name.StartsWith("[Demo]"));

        // Seeding again is a no-op rather than a duplicate.
        var again = await (await admin.PostAsync("/api/v1/admin/demo/seed", null)).Content.ReadFromJsonAsync<DemoDataStatusDto>();
        again!.Users.Should().Be(seeded.Users);

        // Unseed removes it all.
        var cleared = await (await admin.PostAsync("/api/v1/admin/demo/unseed", null)).Content.ReadFromJsonAsync<DemoDataStatusDto>();
        cleared!.Seeded.Should().BeFalse();
        cleared.Users.Should().Be(0);
        cleared.Submissions.Should().Be(0);
        cleared.Discussions.Should().Be(0);
        cleared.Datasets.Should().Be(0);

        (await admin.GetFromJsonAsync<List<DiscussionDto>>("/api/v1/discussions"))!
            .Should().NotContain(d => d.Title.StartsWith("[Demo]"));

        // …and leaves the arena exactly as it found it. Removing the demo people must not take the
        // platform's competitions or their data with them.
        var afterUnseed = await admin.GetFromJsonAsync<List<CompetitionDto>>("/api/v1/competitions");
        afterUnseed!.Count.Should().Be(competitionCount);
        afterUnseed.Should().Contain(c => c.Id == active.Id && c.HasDatasets);
        (await admin.GetFromJsonAsync<List<LeaderboardEntryDto>>($"/api/v1/competitions/{active.Id}/leaderboard"))!
            .Should().BeEmpty();
    }
}
