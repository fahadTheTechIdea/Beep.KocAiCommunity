using Beep.KocAiCommunity.Application.Admin;
using Beep.KocAiCommunity.Application.Security;
using Beep.KocAiCommunity.Contracts.Admin;

namespace Beep.KocAiCommunity.Api.Endpoints;

/// <summary>Platform-admin surface: dashboard, typed settings, feature flags, and the audit trail.</summary>
public static class AdminEndpoints
{
    public static RouteGroupBuilder MapAdminEndpoints(this RouteGroupBuilder group)
    {
        // Everything under here requires the single PlatformAdmin role (403 otherwise).
        var admin = group.MapGroup("/admin").RequireAuthorization(KocPolicies.RequirePlatformAdmin);

        admin.MapGet("/dashboard", async (IAdminDashboardService svc, CancellationToken ct) =>
        {
            var d = await svc.GetDashboardAsync(ct);
            return Results.Ok(new AdminDashboardDto(
                d.Users, d.Workflows, d.Competitions, d.Models, d.Discussions,
                [.. d.RecentAudit.Select(ToAuditDto)],
                [.. d.Health.Select(h => new HealthComponentDto(h.Component, h.Status, h.Detail))]));
        }).WithName("AdminDashboard");

        admin.MapGet("/settings", async (ISettingsService svc, CancellationToken ct) =>
        {
            var items = await svc.GetAllAsync(ct);
            return Results.Ok(items.Select(ToSettingDto).ToList());
        }).WithName("AdminGetSettings");

        admin.MapPut("/settings/{key}", async (string key, UpdateSettingRequest req, IKocCurrentUser me, ISettingsService svc, CancellationToken ct) =>
        {
            try
            {
                var updated = await svc.SetAsync(me.UserId!, key, req.Value ?? "", ct);
                return Results.Ok(ToSettingDto(updated));
            }
            catch (AdminException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).WithName("AdminUpdateSetting");

        admin.MapGet("/feature-flags", async (IFeatureFlagService svc, CancellationToken ct) =>
        {
            var flags = await svc.ListAsync(ct);
            return Results.Ok(flags.Select(f => new FeatureFlagDto(f.Key, f.Name, f.Description, f.IsEnabled, f.RolloutPercentage, f.UpdatedUtc)).ToList());
        }).WithName("AdminListFeatureFlags");

        admin.MapPut("/feature-flags/{key}", async (string key, UpsertFeatureFlagRequest req, IKocCurrentUser me, IFeatureFlagService svc, CancellationToken ct) =>
        {
            try
            {
                var f = await svc.UpsertAsync(me.UserId!, key, req.Name, req.Description ?? "", req.IsEnabled, req.RolloutPercentage, ct);
                return Results.Ok(new FeatureFlagDto(f.Key, f.Name, f.Description, f.IsEnabled, f.RolloutPercentage, f.UpdatedUtc));
            }
            catch (AdminException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).WithName("AdminUpsertFeatureFlag");

        admin.MapGet("/audit", async (string? action, string? actor, int? take, IAdminDashboardService svc, CancellationToken ct) =>
        {
            var rows = await svc.ListAuditAsync(action, actor, take ?? 100, ct);
            return Results.Ok(rows.Select(ToAuditDto).ToList());
        }).WithName("AdminListAudit");

        return group;
    }

    private static SettingDto ToSettingDto(SettingView s) =>
        new(s.Key, s.Category, s.DisplayName, s.Description, s.IsSecret, s.Value, s.IsSet, s.Version, s.UpdatedUtc, s.UpdatedByUserId);

    private static AuditLogDto ToAuditDto(AuditView a) =>
        new(a.Id, a.ActorUserId, a.ActorRole, a.Action, a.Resource, a.ResourceId, a.BeforeJson, a.AfterJson, a.OccurredUtc);
}
