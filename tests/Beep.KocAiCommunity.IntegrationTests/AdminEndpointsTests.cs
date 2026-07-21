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
    [InlineData("/api/v1/admin/users")]
    [InlineData("/api/v1/admin/org-units")]
    public async Task Admin_endpoints_reject_non_admins(string url)
    {
        var employee = _factory.CreateClientAs("plain-emp", "Employee");
        (await employee.GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Granting_and_revoking_competition_creation_controls_access_and_is_listed()
    {
        var admin = _factory.CreateClientAs("rbac-admin", "Employee", "PlatformAdmin");
        var user = _factory.CreateClientAs("rbac-user", competitionCreator: false, "Employee");

        static HttpContent Comp(string scope) =>
            JsonContent.Create(new Contracts.Competitions.CreateCompetitionRequest("C", "d", scope, null, null, 5, "accuracy"));

        // No grant yet → forbidden.
        (await user.PostAsync("/api/v1/competitions", Comp("Group"))).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Admin grants Group.
        (await admin.PutAsJsonAsync("/api/v1/admin/users/rbac-user/competition-grant", new SetCompetitionGrantRequest("Group")))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Group is within the cap → allowed; Company exceeds it → forbidden.
        (await user.PostAsync("/api/v1/competitions", Comp("Group"))).StatusCode.Should().Be(HttpStatusCode.OK);
        (await user.PostAsync("/api/v1/competitions", Comp("Company"))).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var users = await admin.GetFromJsonAsync<List<AdminUserDto>>("/api/v1/admin/users");
        users!.Single(u => u.UserId == "rbac-user").MaxCompetitionScope.Should().Be("Group");

        // Revoke → back to forbidden.
        (await admin.DeleteAsync("/api/v1/admin/users/rbac-user/competition-grant"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await user.PostAsync("/api/v1/competitions", Comp("Group"))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Org_unit_code_is_settable_and_unique()
    {
        var admin = _factory.CreateClientAs("code-admin", "Employee", "PlatformAdmin");

        (await admin.PutAsJsonAsync($"/api/v1/admin/org-units/{_factory.T1}/code", new SetOrgUnitCodeRequest("ZZ9")))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var units = await admin.GetFromJsonAsync<List<OrgUnitCodeDto>>("/api/v1/admin/org-units");
        units!.Single(u => u.Id == _factory.T1).Code.Should().Be("ZZ9");

        // The same code on another unit is rejected.
        (await admin.PutAsJsonAsync($"/api/v1/admin/org-units/{_factory.T2}/code", new SetOrgUnitCodeRequest("ZZ9")))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Setting_a_department_derives_company_and_dept_codes()
    {
        var admin = _factory.CreateClientAs("dept-admin", "Employee", "PlatformAdmin");
        await admin.PutAsJsonAsync($"/api/v1/admin/org-units/{_factory.Company}/code", new SetOrgUnitCodeRequest("KOC"));
        await admin.PutAsJsonAsync($"/api/v1/admin/org-units/{_factory.T1}/code", new SetOrgUnitCodeRequest("RES1"));

        var response = await admin.PutAsJsonAsync("/api/v1/admin/users/dept-user/profile",
            new UpsertUserProfileRequest("dept@koc.com.kw", "Dept User", "RES1"));
        var dto = await response.Content.ReadFromJsonAsync<AdminUserDto>();

        dto!.DepartmentId.Should().Be("RES1");
        dto.DepartmentName.Should().Be("Reservoir Analytics");
        dto.CompanyId.Should().Be("KOC");
        dto.Email.Should().Be("dept@koc.com.kw");
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
