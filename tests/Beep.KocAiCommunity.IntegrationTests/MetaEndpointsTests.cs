using System.Net;
using System.Net.Http.Json;
using Beep.KocAiCommunity.Contracts.Platform;
using FluentAssertions;
using Xunit;

namespace Beep.KocAiCommunity.IntegrationTests;

/// <summary>The anonymous platform-metadata endpoint that drives the demo-environment notice.</summary>
public class MetaEndpointsTests(KocApiFactory factory) : IClassFixture<KocApiFactory>
{
    private readonly KocApiFactory _factory = factory;

    [Fact]
    public async Task Meta_is_anonymous_and_reflects_demo_seed_state()
    {
        var anon = _factory.CreateClientAs(sub: null);

        // Reachable without authentication (unlike the admin-only demo status endpoint).
        (await anon.GetAsync("/api/v1/meta")).StatusCode.Should().Be(HttpStatusCode.OK);

        var admin = _factory.CreateClientAs("meta-admin", "Employee", "PlatformAdmin");

        await admin.PostAsync("/api/v1/admin/demo/unseed", null);
        var beforeSeed = await anon.GetFromJsonAsync<PlatformMetaDto>("/api/v1/meta");
        beforeSeed!.DemoDataSeeded.Should().BeFalse();

        await admin.PostAsync("/api/v1/admin/demo/seed", null);
        var afterSeed = await anon.GetFromJsonAsync<PlatformMetaDto>("/api/v1/meta");
        afterSeed!.DemoDataSeeded.Should().BeTrue();

        // The test host configures neither Windows SSO nor Entra, so this is a demo build.
        afterSeed.DemoMode.Should().BeTrue();

        await admin.PostAsync("/api/v1/admin/demo/unseed", null);
        (await anon.GetFromJsonAsync<PlatformMetaDto>("/api/v1/meta"))!.DemoDataSeeded.Should().BeFalse();
    }
}
