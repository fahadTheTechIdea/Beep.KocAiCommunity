using System.Net;
using System.Net.Http.Json;
using Beep.KocAiCommunity.Contracts.Admin;
using Beep.KocAiCommunity.Contracts.Identity;
using FluentAssertions;
using Xunit;

namespace Beep.KocAiCommunity.IntegrationTests;

/// <summary>
/// The API booted the way a KOC deployment runs it: IIS has already verified the Windows account, the
/// Web vouches for that person, and the platform's database decides what they may do. The test scheme
/// stands in for IIS — it establishes an identity, and any roles it asserts must be disregarded.
/// </summary>
public sealed class ExternalIdentityApiFactory : KocApiFactory
{
    protected override string AuthMode => "KocEnvironment";
}

public class ExternalSignInRolesTests
{
    [Fact]
    public async Task A_first_time_visitor_is_recorded_and_the_first_one_administers_the_platform()
    {
        using var factory = new ExternalIdentityApiFactory();

        // Nobody has signed in yet, so the first arrival from the corporate directory runs the platform —
        // otherwise a fresh KOC install would have no administrator and no way to appoint one.
        var first = factory.CreateClientAsUnknownUser("KOC-aldhubaib");
        var me = await first.GetFromJsonAsync<MeResponse>("/api/v1/me");
        me!.Roles.Should().Contain("PlatformAdmin");

        // A later colleague is an ordinary member.
        var second = factory.CreateClientAsUnknownUser("KOC-someone-else");
        var theirMe = await second.GetFromJsonAsync<MeResponse>("/api/v1/me");
        theirMe!.Roles.Should().BeEquivalentTo(["Employee"]);

        // Both are now in the RBAC console without an administrator pre-creating them.
        var users = await first.GetFromJsonAsync<List<AdminUserDto>>("/api/v1/admin/users");
        users!.Select(u => u.UserId).Should().Contain(["KOC-aldhubaib", "KOC-someone-else"]);
    }

    [Fact]
    public async Task Roles_claimed_by_the_identity_provider_are_ignored_in_favour_of_the_database()
    {
        using var factory = new ExternalIdentityApiFactory();
        await factory.CreateClientAsUnknownUser("KOC-first").GetAsync("/api/v1/me");   // claim the admin slot

        // The provider asserts PlatformAdmin for someone the platform records as a plain Employee.
        // Trusting it would let whoever configures the directory grant themselves the platform.
        var impostor = factory.CreateClientAsUnknownUser("KOC-impostor", "PlatformAdmin", "CompetitionAdmin");

        var me = await impostor.GetFromJsonAsync<MeResponse>("/api/v1/me");
        me!.Roles.Should().BeEquivalentTo(["Employee"]);
        (await impostor.GetAsync("/api/v1/admin/users")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task An_administrator_grants_roles_to_a_corporate_account_from_the_console()
    {
        using var factory = new ExternalIdentityApiFactory();
        var admin = factory.CreateClientAsUnknownUser("KOC-admin");
        await admin.GetAsync("/api/v1/me");                                   // first arrival → admin

        var colleague = factory.CreateClientAsUnknownUser("KOC-colleague");
        await colleague.GetAsync("/api/v1/me");                               // recorded as Employee

        var granted = await admin.PutAsJsonAsync("/api/v1/admin/users/KOC-colleague/roles",
            new SetUserRolesRequest(["Manager", "CompetitionAdmin"]));
        granted.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Effective immediately — the corporate account presents no new credential, and authorization is
        // read from the database on each request.
        var me = await colleague.GetFromJsonAsync<MeResponse>("/api/v1/me");
        me!.Roles.Should().BeEquivalentTo(["Manager", "CompetitionAdmin"]);
        me.PositionLevel.Should().Be("Manager");
    }
}
