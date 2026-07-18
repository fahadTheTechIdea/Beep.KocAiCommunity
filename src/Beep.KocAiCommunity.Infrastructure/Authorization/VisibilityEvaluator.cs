using Beep.KocAiCommunity.Application.Authorization;
using Beep.KocAiCommunity.Application.Security;
using Beep.KocAiCommunity.Domain.Organization;
using Beep.KocAiCommunity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Beep.KocAiCommunity.Infrastructure.Authorization;

/// <summary>
/// Enforces org-scoped visibility. Company scope is visible to every KOC user; otherwise the
/// user's home unit must lie within the visibility unit's subtree (materialized-path prefix).
/// </summary>
public sealed class VisibilityEvaluator(KocDbContext db, IKocCurrentUser? currentUser = null) : IVisibilityEvaluator
{
    public async Task<bool> CanSeeAsync(string userId, VisibilityScope scope, Guid visibilityOrgUnitId, CancellationToken ct = default)
    {
        if (scope == VisibilityScope.Company)
        {
            return true;
        }

        // PlatformAdmin sees everything.
        if (currentUser is not null && currentUser.UserId == userId && currentUser.IsInRole(KocRoles.PlatformAdmin))
        {
            return true;
        }

        var visibilityPath = await db.OrgUnits
            .Where(u => u.Id == visibilityOrgUnitId)
            .Select(u => u.Path)
            .FirstOrDefaultAsync(ct);

        if (visibilityPath is null)
        {
            return false;
        }

        var homePath = await db.OrgMemberships
            .Where(m => m.UserId == userId && m.IsPrimary && m.ToUtc == null)
            .Join(db.OrgUnits, m => m.OrgUnitId, u => u.Id, (_, u) => u.Path)
            .FirstOrDefaultAsync(ct);

        if (homePath is null)
        {
            return false;
        }

        var prefix = visibilityPath.EndsWith('/') ? visibilityPath : visibilityPath + "/";
        return homePath == visibilityPath || homePath.StartsWith(prefix, StringComparison.Ordinal);
    }
}
