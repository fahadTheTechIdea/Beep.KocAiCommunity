using Beep.KocAiCommunity.Application.Audit;
using Beep.KocAiCommunity.Infrastructure.Admin;
using FluentAssertions;
using Xunit;

namespace Beep.KocAiCommunity.UnitTests;

public class FeatureFlagServiceTests
{
    private sealed class NoopAudit : IAuditEnvelope
    {
        public Task WriteAsync(AuditEntry entry, CancellationToken ct = default) => Task.CompletedTask;
    }

    private static FeatureFlagService Build(OrgTestContext ctx) => new(ctx.Db, new NoopAudit());

    [Fact]
    public async Task Disabled_flag_is_off_for_everyone()
    {
        using var ctx = new OrgTestContext();
        var svc = Build(ctx);
        await svc.UpsertAsync("admin", "new-ui", "New UI", "", isEnabled: false, rolloutPercentage: 100);

        (await svc.IsEnabledAsync("new-ui", "emp1")).Should().BeFalse();
    }

    [Fact]
    public async Task Full_rollout_is_on_and_zero_rollout_is_off()
    {
        using var ctx = new OrgTestContext();
        var svc = Build(ctx);
        await svc.UpsertAsync("admin", "full", "Full", "", isEnabled: true, rolloutPercentage: 100);
        await svc.UpsertAsync("admin", "none", "None", "", isEnabled: true, rolloutPercentage: 0);

        (await svc.IsEnabledAsync("full", "emp1")).Should().BeTrue();
        (await svc.IsEnabledAsync("none", "emp1")).Should().BeFalse();
    }

    [Fact]
    public async Task Rollout_membership_is_stable_per_user()
    {
        using var ctx = new OrgTestContext();
        var svc = Build(ctx);
        await svc.UpsertAsync("admin", "half", "Half", "", isEnabled: true, rolloutPercentage: 50);

        var first = await svc.IsEnabledAsync("half", "emp1");
        var second = await svc.IsEnabledAsync("half", "emp1");
        first.Should().Be(second); // deterministic bucket — same user always lands the same way

        // Across many users the split is neither all-in nor all-out.
        var results = new List<bool>();
        for (var i = 0; i < 200; i++)
        {
            results.Add(await svc.IsEnabledAsync("half", $"user-{i}"));
        }

        results.Should().Contain(true).And.Contain(false);
    }

    [Fact]
    public async Task Rollout_is_clamped_to_0_100()
    {
        using var ctx = new OrgTestContext();
        var svc = Build(ctx);
        var flag = await svc.UpsertAsync("admin", "clamp", "Clamp", "", isEnabled: true, rolloutPercentage: 250);
        flag.RolloutPercentage.Should().Be(100);
    }
}
