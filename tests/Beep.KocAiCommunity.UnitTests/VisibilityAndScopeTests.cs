using Beep.KocAiCommunity.Domain.Organization;
using Beep.KocAiCommunity.Infrastructure.Authorization;
using Beep.KocAiCommunity.Infrastructure.Organization;
using FluentAssertions;
using Xunit;

namespace Beep.KocAiCommunity.UnitTests;

public class VisibilityAndScopeTests
{
    [Fact]
    public async Task Group_scoped_resource_is_visible_within_that_group_only()
    {
        using var ctx = new OrgTestContext();
        var evaluator = new VisibilityEvaluator(ctx.Db);

        // Resource visible to Group G1.
        (await evaluator.CanSeeAsync("emp1", VisibilityScope.Group, ctx.G1)).Should().BeTrue();  // in T1 ⊂ G1
        (await evaluator.CanSeeAsync("emp2", VisibilityScope.Group, ctx.G1)).Should().BeTrue();  // in T2 ⊂ G1
        (await evaluator.CanSeeAsync("empOther", VisibilityScope.Group, ctx.G1)).Should().BeFalse(); // in D2
    }

    [Fact]
    public async Task Team_scoped_resource_is_invisible_to_a_peer_team()
    {
        using var ctx = new OrgTestContext();
        var evaluator = new VisibilityEvaluator(ctx.Db);

        (await evaluator.CanSeeAsync("emp1", VisibilityScope.Team, ctx.T1)).Should().BeTrue();
        (await evaluator.CanSeeAsync("emp2", VisibilityScope.Team, ctx.T1)).Should().BeFalse();
    }

    [Fact]
    public async Task Company_scoped_resource_is_visible_to_everyone()
    {
        using var ctx = new OrgTestContext();
        var evaluator = new VisibilityEvaluator(ctx.Db);

        (await evaluator.CanSeeAsync("empOther", VisibilityScope.Company, ctx.Company)).Should().BeTrue();
    }

    [Fact]
    public async Task Manager_scope_covers_their_group_subtree_and_members()
    {
        using var ctx = new OrgTestContext();
        var resolver = new OrgScopeResolver(ctx.Db);

        var scope = await resolver.ResolveForUserAsync("mgr1");

        scope.OrgUnitIds.Should().BeEquivalentTo([ctx.G1, ctx.T1, ctx.T2]);
        scope.MemberUserIds.Should().Contain(["emp1", "emp2", "lead1", "mgr1"]);
        scope.MemberUserIds.Should().NotContain("empOther");
    }

    [Fact]
    public async Task Employee_scope_is_themselves_only()
    {
        using var ctx = new OrgTestContext();
        var resolver = new OrgScopeResolver(ctx.Db);

        var scope = await resolver.ResolveForUserAsync("emp1");

        scope.OrgUnitIds.Should().BeEmpty();
        scope.MemberUserIds.Should().BeEquivalentTo(["emp1"]);
    }

    [Fact]
    public async Task Supervisor_cannot_reach_outside_their_subtree()
    {
        using var ctx = new OrgTestContext();
        var resolver = new OrgScopeResolver(ctx.Db);

        (await resolver.IsWithinScopeAsync("mgr1", ctx.T1)).Should().BeTrue();
        (await resolver.IsWithinScopeAsync("mgr1", ctx.T9)).Should().BeFalse(); // T9 is under D2
    }
}
