using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Beep.KocAiCommunity.Contracts.Admin;
using Beep.KocAiCommunity.Contracts.Identity;
using FluentAssertions;
using Xunit;

namespace Beep.KocAiCommunity.IntegrationTests;

/// <summary>
/// Authentication varies by deployment; authorization does not. These pin the rule that the platform's
/// own database decides what a caller may do — so an administrator can manage roles in the RBAC console
/// whether people sign in with a password here, through the corporate network, or via Entra.
/// </summary>
public class AppOwnedRolesTests
{
    private const string GoodPassword = "Wells-2026!";

    [Fact]
    public async Task Roles_assigned_in_the_console_take_effect_without_re_registering()
    {
        using var factory = new RealTokenApiFactory();

        var admin = await Register(factory, "admin@koc.com", "Admin");          // first account → PlatformAdmin
        var member = await Register(factory, "member@koc.com", "Member");       // → Employee

        var adminClient = Authenticated(factory, admin.AccessToken);
        var users = await adminClient.GetFromJsonAsync<List<AdminUserDto>>("/api/v1/admin/users");
        users!.Single(u => u.UserId == member.UserId).Roles.Should().BeEquivalentTo(["Employee"]);

        // The member cannot reach an admin-only endpoint...
        var memberClient = Authenticated(factory, member.AccessToken);
        (await memberClient.GetAsync("/api/v1/admin/users")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // ...until an administrator grants the role in the console.
        var granted = await adminClient.PutAsJsonAsync($"/api/v1/admin/users/{member.UserId}/roles",
            new SetUserRolesRequest(["Employee", "PlatformAdmin"]));
        granted.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // The same token now carries the new role: authorization is read from the database per request,
        // so a role change doesn't wait for the user to sign in again.
        (await memberClient.GetAsync("/api/v1/admin/users")).StatusCode.Should().Be(HttpStatusCode.OK);

        var me = await memberClient.GetFromJsonAsync<MeResponse>("/api/v1/me");
        me!.Roles.Should().Contain("PlatformAdmin");
    }

    [Fact]
    public async Task The_last_platform_admin_cannot_be_demoted()
    {
        using var factory = new RealTokenApiFactory();
        var admin = await Register(factory, "only.admin@koc.com", "Only Admin");
        var client = Authenticated(factory, admin.AccessToken);

        var response = await client.PutAsJsonAsync($"/api/v1/admin/users/{admin.UserId}/roles",
            new SetUserRolesRequest(["Employee"]));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, "locking everyone out would need a database edit to undo");
        (await client.GetAsync("/api/v1/admin/users")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task An_unknown_role_is_refused_rather_than_silently_dropped()
    {
        using var factory = new RealTokenApiFactory();
        var admin = await Register(factory, "admin2@koc.com", "Admin Two");
        var client = Authenticated(factory, admin.AccessToken);

        var response = await client.PutAsJsonAsync($"/api/v1/admin/users/{admin.UserId}/roles",
            new SetUserRolesRequest(["PlatformAdmin", "SuperUser"]));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task The_assignable_roles_match_the_authorization_policies()
    {
        using var factory = new RealTokenApiFactory();
        var admin = await Register(factory, "admin3@koc.com", "Admin Three");

        var roles = await Authenticated(factory, admin.AccessToken)
            .GetFromJsonAsync<AssignableRolesDto>("/api/v1/admin/roles");

        roles!.Positions.Should().BeEquivalentTo(["Employee", "TeamLeader", "Manager", "DCEO", "CEO"]);
        roles.Functions.Should().BeEquivalentTo(["PlatformAdmin", "CompetitionAdmin", "LearningAdmin", "Auditor"]);
    }

    private static async Task<AuthTokenResponse> Register(RealTokenApiFactory factory, string email, string displayName)
    {
        var response = await factory.CreateAnonymousClient()
            .PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest(email, GoodPassword, displayName));
        response.StatusCode.Should().Be(HttpStatusCode.OK, "but said: {0}", await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<AuthTokenResponse>())!;
    }

    private static HttpClient Authenticated(RealTokenApiFactory factory, string token)
    {
        var client = factory.CreateAnonymousClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
