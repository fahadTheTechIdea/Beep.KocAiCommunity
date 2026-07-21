using Beep.KocAiCommunity.Application.Admin;
using Beep.KocAiCommunity.Application.Security;
using Beep.KocAiCommunity.Contracts.Admin;
using Beep.KocAiCommunity.Domain.Organization;

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

        // Demo data: seed a full explorable demo (people, engagement, competition, discussion, dataset)
        // or remove it again. Everything is namespaced to demo-* so real KOC data is untouched.
        admin.MapGet("/demo", async (IDemoDataService svc, CancellationToken ct) => Results.Ok(ToDemoDto(await svc.GetStatusAsync(ct))))
            .WithName("AdminDemoStatus");

        admin.MapPost("/demo/seed", async (IKocCurrentUser me, IDemoDataService svc, CancellationToken ct) =>
            Results.Ok(ToDemoDto(await svc.SeedAsync(me.UserId!, ct))))
            .WithName("AdminSeedDemo");

        admin.MapPost("/demo/unseed", async (IKocCurrentUser me, IDemoDataService svc, CancellationToken ct) =>
            Results.Ok(ToDemoDto(await svc.UnseedAsync(me.UserId!, ct))))
            .WithName("AdminUnseedDemo");

        admin.MapGet("/audit", async (string? action, string? actor, int? take, IAdminDashboardService svc, CancellationToken ct) =>
        {
            var rows = await svc.ListAuditAsync(action, actor, take ?? 100, ct);
            return Results.Ok(rows.Select(ToAuditDto).ToList());
        }).WithName("AdminListAudit");

        // RBAC / Users: who exists, their org identity, and their competition-creation rights.
        admin.MapGet("/users", async (IAccessAdminService svc, CancellationToken ct) =>
        {
            var users = await svc.ListUsersAsync(ct);
            return Results.Ok(users.Select(ToUserDto).ToList());
        }).WithName("AdminListUsers");

        admin.MapGet("/org-units", async (IAccessAdminService svc, CancellationToken ct) =>
        {
            var units = await svc.ListOrgUnitsAsync(ct);
            return Results.Ok(units.Select(u => new OrgUnitCodeDto(u.Id, u.Name, u.Type.ToString(), u.Path, u.Code)).ToList());
        }).WithName("AdminListOrgUnits");

        admin.MapPut("/users/{userId}/profile", async (string userId, UpsertUserProfileRequest req, IAccessAdminService svc, CancellationToken ct) =>
        {
            try
            {
                return Results.Ok(ToUserDto(await svc.UpsertProfileAsync(userId, req.Email, req.DisplayName, req.DepartmentCode, ct)));
            }
            catch (AccessAdminException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).WithName("AdminUpsertUserProfile");

        admin.MapPut("/users/{userId}/competition-grant", async (string userId, SetCompetitionGrantRequest req, IAccessAdminService svc, CancellationToken ct) =>
        {
            if (!Enum.TryParse<VisibilityScope>(req.MaxScope, ignoreCase: true, out var scope))
            {
                return Results.BadRequest(new { error = $"Unknown scope '{req.MaxScope}'." });
            }

            await svc.SetCompetitionGrantAsync(userId, scope, ct);
            return Results.NoContent();
        }).WithName("AdminSetCompetitionGrant");

        admin.MapDelete("/users/{userId}/competition-grant", async (string userId, IAccessAdminService svc, CancellationToken ct) =>
        {
            await svc.RevokeCompetitionGrantAsync(userId, ct);
            return Results.NoContent();
        }).WithName("AdminRevokeCompetitionGrant");

        admin.MapPut("/org-units/{id:guid}/code", async (Guid id, SetOrgUnitCodeRequest req, IAccessAdminService svc, CancellationToken ct) =>
        {
            try
            {
                await svc.SetOrgUnitCodeAsync(id, req.Code, ct);
                return Results.NoContent();
            }
            catch (AccessAdminException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).WithName("AdminSetOrgUnitCode");

        return group;
    }

    private static AdminUserDto ToUserDto(AccessUserView u) =>
        new(u.UserId, u.Email, u.DisplayName, u.CompanyId, u.DepartmentId, u.DepartmentName,
            u.PositionLevel.ToString(), u.MaxCompetitionScope?.ToString());

    private static DemoDataStatusDto ToDemoDto(DemoDataStatus s) =>
        new(s.Seeded, s.Users, s.Competitions, s.Discussions, s.Datasets);

    private static SettingDto ToSettingDto(SettingView s) =>
        new(s.Key, s.Category, s.DisplayName, s.Description, s.IsSecret, s.Value, s.IsSet, s.Version, s.UpdatedUtc, s.UpdatedByUserId);

    private static AuditLogDto ToAuditDto(AuditView a) =>
        new(a.Id, a.ActorUserId, a.ActorRole, a.Action, a.Resource, a.ResourceId, a.BeforeJson, a.AfterJson, a.OccurredUtc);
}
