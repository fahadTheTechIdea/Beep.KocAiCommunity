using Beep.KocAiCommunity.Application.Organization;
using Beep.KocAiCommunity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Beep.KocAiCommunity.Infrastructure.Organization;

/// <summary>
/// Resolves the supervisory subtree via the materialized <see cref="Domain.Organization.OrgUnit.Path"/>.
/// A leader's subtree is every unit whose path is at or below the unit they lead; an Employee
/// (leads nothing) resolves to only themselves.
/// </summary>
public sealed class OrgScopeResolver(KocDbContext db) : IOrgScopeResolver
{
    public async Task<OrgScope> ResolveForUserAsync(string userId, CancellationToken ct = default)
    {
        var ledPath = await db.OrgUnits
            .Where(u => u.LeaderUserId == userId)
            .OrderBy(u => u.Type)
            .Select(u => u.Path)
            .FirstOrDefaultAsync(ct);

        if (ledPath is null)
        {
            // Not a supervisor — scope is the user alone.
            return new OrgScope([], [userId]);
        }

        var prefix = ledPath.EndsWith('/') ? ledPath : ledPath + "/";

        var unitIds = await db.OrgUnits
            .Where(u => u.Path == ledPath || u.Path.StartsWith(prefix))
            .Select(u => u.Id)
            .ToListAsync(ct);

        var memberIds = await db.OrgMemberships
            .Where(m => m.ToUtc == null && unitIds.Contains(m.OrgUnitId))
            .Select(m => m.UserId)
            .Distinct()
            .ToListAsync(ct);

        return new OrgScope(unitIds, memberIds);
    }

    public async Task<bool> IsWithinScopeAsync(string userId, Guid orgUnitId, CancellationToken ct = default)
    {
        var scope = await ResolveForUserAsync(userId, ct);
        return scope.OrgUnitIds.Contains(orgUnitId);
    }
}
