using System.Net;
using System.Net.Http.Json;
using Beep.KocAiCommunity.Contracts.Identity;
using Beep.KocAiCommunity.Contracts.Organization;
using FluentAssertions;
using Xunit;

namespace Beep.KocAiCommunity.IntegrationTests;

public class ApiEndpointsTests(KocApiFactory factory) : IClassFixture<KocApiFactory>
{
    private readonly KocApiFactory _factory = factory;

    [Fact]
    public async Task Me_requires_authentication()
    {
        var client = _factory.CreateClientAs(sub: null);
        var response = await client.GetAsync("/api/v1/me");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Me_returns_position_and_home_unit()
    {
        var client = _factory.CreateClientAs("emp1", "Employee");
        var me = await client.GetFromJsonAsync<MeResponse>("/api/v1/me");

        me.Should().NotBeNull();
        me!.UserId.Should().Be("emp1");
        me.PositionLevel.Should().Be("Employee");
        me.HomeOrgUnitId.Should().Be(_factory.T1);
        me.LedOrgUnitId.Should().BeNull();
    }

    [Fact]
    public async Task Employee_scope_is_self_only()
    {
        var client = _factory.CreateClientAs("emp1", "Employee");
        var scope = await client.GetFromJsonAsync<OrgScopeDto>("/api/v1/me/scope");

        scope!.OrgUnitIds.Should().BeEmpty();
        scope.MemberCount.Should().Be(1);
    }

    [Fact]
    public async Task Manager_scope_covers_group_subtree()
    {
        var client = _factory.CreateClientAs("mgr1", "Manager");
        var scope = await client.GetFromJsonAsync<OrgScopeDto>("/api/v1/me/scope");

        scope!.OrgUnitIds.Should().BeEquivalentTo([_factory.G1, _factory.T1, _factory.T2]);
        scope.MemberCount.Should().Be(3); // emp1, emp2, mgr1
    }

    [Fact]
    public async Task Supervision_scope_forbidden_for_employee_allowed_for_manager()
    {
        var employee = _factory.CreateClientAs("emp1", "Employee");
        (await employee.GetAsync("/api/v1/supervision/scope")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var manager = _factory.CreateClientAs("mgr1", "Manager");
        (await manager.GetAsync("/api/v1/supervision/scope")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Audience_preview_counts_the_group_subtree()
    {
        var client = _factory.CreateClientAs("emp1", "Employee");
        var audience = await client.GetFromJsonAsync<AudienceDto>($"/api/v1/org/units/{_factory.G1}/audience?scope=group");

        audience!.UserCount.Should().Be(3); // emp1 (T1), emp2 (T2), mgr1 (G1)
    }

    [Fact]
    public async Task Org_units_can_be_browsed()
    {
        var client = _factory.CreateClientAs("emp1", "Employee");
        var units = await client.GetFromJsonAsync<List<OrgUnitDto>>("/api/v1/org/units");

        units.Should().NotBeNullOrEmpty();
        units!.Should().Contain(u => u.Type == "Company");
    }
}
