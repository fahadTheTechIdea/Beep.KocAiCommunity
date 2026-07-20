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

        var seeded = await (await admin.PostAsync("/api/v1/admin/demo/seed", null)).Content.ReadFromJsonAsync<DemoDataStatusDto>();
        seeded!.Seeded.Should().BeTrue();
        seeded.Users.Should().BeGreaterThan(0);
        seeded.Competitions.Should().BeGreaterThan(0);

        // The demo content is actually visible through the normal, company-scoped surfaces.
        var competitions = await admin.GetFromJsonAsync<List<CompetitionDto>>("/api/v1/competitions");
        competitions.Should().Contain(c => c.Title.StartsWith("[Demo]"));
        var discussions = await admin.GetFromJsonAsync<List<DiscussionDto>>("/api/v1/discussions");
        discussions.Should().Contain(d => d.Title.StartsWith("[Demo]"));

        // Seeding again is a no-op rather than a duplicate.
        var again = await (await admin.PostAsync("/api/v1/admin/demo/seed", null)).Content.ReadFromJsonAsync<DemoDataStatusDto>();
        again!.Users.Should().Be(seeded.Users);

        // Unseed removes it all.
        var cleared = await (await admin.PostAsync("/api/v1/admin/demo/unseed", null)).Content.ReadFromJsonAsync<DemoDataStatusDto>();
        cleared!.Seeded.Should().BeFalse();
        cleared.Users.Should().Be(0);
        cleared.Competitions.Should().Be(0);

        (await admin.GetFromJsonAsync<List<DiscussionDto>>("/api/v1/discussions"))!
            .Should().NotContain(d => d.Title.StartsWith("[Demo]"));
    }
}
