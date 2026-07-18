using Beep.KocAiCommunity.Application.Organization;
using Beep.KocAiCommunity.Application.Security;
using Beep.KocAiCommunity.Application.Supervision;
using Beep.KocAiCommunity.Contracts.Datasets;
using Beep.KocAiCommunity.Contracts.Organization;
using Beep.KocAiCommunity.Domain.Organization;
using Beep.KocAiCommunity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Beep.KocAiCommunity.Api.Endpoints;

public static class OrgEndpoints
{
    public static RouteGroupBuilder MapOrgEndpoints(this RouteGroupBuilder group)
    {
        // Browse the org tree (for visibility pickers).
        group.MapGet("/org/units", async (Guid? parentId, string? type, KocDbContext db, CancellationToken ct) =>
        {
            var query = db.OrgUnits.AsNoTracking().AsQueryable();

            if (parentId is { } pid)
            {
                query = query.Where(u => u.ParentId == pid);
            }

            if (!string.IsNullOrWhiteSpace(type) && Enum.TryParse<OrgUnitType>(type, ignoreCase: true, out var t))
            {
                query = query.Where(u => u.Type == t);
            }

            var units = await query
                .OrderBy(u => u.Path)
                .Select(u => new OrgUnitDto(u.Id, u.Name, u.Type.ToString(), u.ParentId, u.Path))
                .ToListAsync(ct);

            return Results.Ok(units);
        })
        .WithName("BrowseOrgUnits")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        // Audience-count preview for a "who can see this" choice.
        group.MapGet("/org/units/{id:guid}/audience", async (Guid id, string? scope, KocDbContext db, CancellationToken ct) =>
        {
            if (string.Equals(scope, "company", StringComparison.OrdinalIgnoreCase))
            {
                var all = await db.OrgMemberships.CountAsync(m => m.IsPrimary && m.ToUtc == null, ct);
                return Results.Ok(new AudienceDto("Company", id, all));
            }

            var path = await db.OrgUnits.Where(u => u.Id == id).Select(u => u.Path).FirstOrDefaultAsync(ct);
            if (path is null)
            {
                return Results.NotFound();
            }

            var prefix = path.EndsWith('/') ? path : path + "/";
            var count = await db.OrgMemberships
                .Where(m => m.IsPrimary && m.ToUtc == null)
                .Join(db.OrgUnits, m => m.OrgUnitId, u => u.Id, (_, u) => u.Path)
                .CountAsync(p => p == path || p.StartsWith(prefix), ct);

            return Results.Ok(new AudienceDto(scope ?? "Team", id, count));
        })
        .WithName("AudiencePreview")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        // Supervisor rollup scope — only positions that lead a unit; scoped to their subtree.
        group.MapGet("/supervision/scope", async (IKocCurrentUser me, Application.Organization.IOrgScopeResolver resolver, CancellationToken ct) =>
        {
            var scope = await resolver.ResolveForUserAsync(me.UserId!, ct);
            return Results.Ok(new OrgScopeDto([.. scope.OrgUnitIds], scope.MemberUserIds.Count));
        })
        .WithName("SupervisionScope")
        .RequireAuthorization(KocPolicies.RequireSupervisor);

        // Visibility picker: the caller's resolved unit + audience count for a chosen scope.
        group.MapGet("/me/audience", async (string scope, IKocCurrentUser me, IOrgDirectory directory, KocDbContext db, CancellationToken ct) =>
        {
            if (!Enum.TryParse<VisibilityScope>(scope, ignoreCase: true, out var s))
            {
                return Results.BadRequest(new { error = $"Unknown scope '{scope}'." });
            }

            if (s == VisibilityScope.Company)
            {
                var all = await db.OrgMemberships.CountAsync(m => m.IsPrimary && m.ToUtc == null, ct);
                return Results.Ok(new VisibilityOptionDto("Company", null, "All KOC", all));
            }

            var unitId = await directory.ResolveScopeUnitAsync(me.UserId!, s, ct);
            if (unitId is null)
            {
                return Results.Ok(new VisibilityOptionDto(s.ToString(), null, "(no unit at this level)", 0));
            }

            var unit = await db.OrgUnits.Where(u => u.Id == unitId).Select(u => new { u.Name, u.Path }).FirstAsync(ct);
            var prefix = unit.Path + "/";
            var count = await db.OrgMemberships
                .Where(m => m.IsPrimary && m.ToUtc == null)
                .Join(db.OrgUnits, m => m.OrgUnitId, u => u.Id, (_, u) => u.Path)
                .CountAsync(p => p == unit.Path || p.StartsWith(prefix), ct);

            return Results.Ok(new VisibilityOptionDto(s.ToString(), unitId, unit.Name, count));
        })
        .WithName("MyAudience")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        // Read-only participation rollup for the caller's org subtree.
        group.MapGet("/supervision/rollup", async (IKocCurrentUser me, ISupervisionService supervision, CancellationToken ct) =>
        {
            var rollup = await supervision.GetRollupAsync(me.UserId!, ct);
            return Results.Ok(rollup);
        })
        .WithName("SupervisionRollup")
        .RequireAuthorization(KocPolicies.RequireSupervisor);

        return group;
    }
}
