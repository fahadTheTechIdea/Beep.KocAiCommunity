using System.Net;
using System.Net.Http.Json;
using Beep.KocAiCommunity.Contracts.Admin;
using FluentAssertions;
using Xunit;

namespace Beep.KocAiCommunity.IntegrationTests;

public class AdminEndpointsTests(KocApiFactory factory) : IClassFixture<KocApiFactory>
{
    private readonly KocApiFactory _factory = factory;

    [Theory]
    [InlineData("/api/v1/admin/dashboard")]
    [InlineData("/api/v1/admin/settings")]
    [InlineData("/api/v1/admin/feature-flags")]
    [InlineData("/api/v1/admin/audit")]
    public async Task Admin_endpoints_reject_non_admins(string url)
    {
        var employee = _factory.CreateClientAs("plain-emp", "Employee");
        (await employee.GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Setting_update_persists_bumps_version_and_is_audited()
    {
        var admin = _factory.CreateClientAs("admin1", "Employee", "PlatformAdmin");

        var updated = await (await admin.PutAsJsonAsync("/api/v1/admin/settings/general.platformName",
            new UpdateSettingRequest("KOC AI Hub"))).Content.ReadFromJsonAsync<SettingDto>();
        updated!.Value.Should().Be("KOC AI Hub");
        updated.Version.Should().Be(1);

        // A second update bumps the version.
        var again = await (await admin.PutAsJsonAsync("/api/v1/admin/settings/general.platformName",
            new UpdateSettingRequest("KOC AI Portal"))).Content.ReadFromJsonAsync<SettingDto>();
        again!.Version.Should().Be(2);

        // The change shows up in GET and in the audit trail.
        var settings = await admin.GetFromJsonAsync<List<SettingDto>>("/api/v1/admin/settings");
        settings!.Single(s => s.Key == "general.platformName").Value.Should().Be("KOC AI Portal");

        var audit = await admin.GetFromJsonAsync<List<AuditLogDto>>("/api/v1/admin/audit?action=setting.update");
        audit!.Should().Contain(a => a.ResourceId == "general.platformName");
    }

    [Fact]
    public async Task Secret_settings_are_masked_in_responses_and_audit()
    {
        var admin = _factory.CreateClientAs("admin2", "Employee", "PlatformAdmin");
        const string secret = "sup3r-s3cret-smtp-pw";

        var updated = await (await admin.PutAsJsonAsync("/api/v1/admin/settings/email.smtpPassword",
            new UpdateSettingRequest(secret))).Content.ReadFromJsonAsync<SettingDto>();
        updated!.IsSecret.Should().BeTrue();
        updated.Value.Should().NotContain(secret); // masked, never the plaintext
        updated.IsSet.Should().BeTrue();

        var settings = await admin.GetFromJsonAsync<List<SettingDto>>("/api/v1/admin/settings");
        settings!.Single(s => s.Key == "email.smtpPassword").Value.Should().NotContain(secret);

        // The plaintext must never appear anywhere in the audit log JSON.
        var audit = await admin.GetFromJsonAsync<List<AuditLogDto>>("/api/v1/admin/audit");
        audit!.Should().NotContain(a => (a.AfterJson ?? "").Contains(secret) || (a.BeforeJson ?? "").Contains(secret));
    }

    [Fact]
    public async Task Feature_flag_upserts_and_lists()
    {
        var admin = _factory.CreateClientAs("admin3", "Employee", "PlatformAdmin");
        var f = await (await admin.PutAsJsonAsync("/api/v1/admin/feature-flags/beta-workflows",
            new UpsertFeatureFlagRequest("Beta workflows", "New designer", true, 60))).Content.ReadFromJsonAsync<FeatureFlagDto>();
        f!.IsEnabled.Should().BeTrue();
        f.RolloutPercentage.Should().Be(60);

        var flags = await admin.GetFromJsonAsync<List<FeatureFlagDto>>("/api/v1/admin/feature-flags");
        flags!.Should().Contain(x => x.Key == "beta-workflows");
    }

    [Fact]
    public async Task Dashboard_returns_live_counts_and_health()
    {
        var admin = _factory.CreateClientAs("admin4", "Employee", "PlatformAdmin");
        var d = await admin.GetFromJsonAsync<AdminDashboardDto>("/api/v1/admin/dashboard");
        d!.Health.Should().Contain(h => h.Component == "Database" && h.Status == "Healthy");
        d.Users.Should().BeGreaterThanOrEqualTo(0);
    }
}
