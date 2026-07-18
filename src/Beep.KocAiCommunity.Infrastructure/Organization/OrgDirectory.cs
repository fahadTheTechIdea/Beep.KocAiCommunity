using Beep.KocAiCommunity.Application.Organization;
using Beep.KocAiCommunity.Domain.Organization;
using Beep.KocAiCommunity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Beep.KocAiCommunity.Infrastructure.Organization;

/// <summary>Resolves a person's home unit, position, and led unit from the org tables.</summary>
public sealed class OrgDirectory(KocDbContext db) : IOrgDirectory
{
    public async Task<OrgProfile?> GetProfileAsync(string userId, CancellationToken ct = default)
    {
        var primary = await db.OrgMemberships
            .Where(m => m.UserId == userId && m.IsPrimary && m.ToUtc == null)
            .OrderByDescending(m => m.FromUtc)
            .FirstOrDefaultAsync(ct);

        // The unit a leader leads (highest, i.e. broadest, wins if more than one).
        var led = await db.OrgUnits
            .Where(u => u.LeaderUserId == userId)
            .OrderBy(u => u.Type)
            .FirstOrDefaultAsync(ct);

        if (primary is null && led is null)
        {
            return null;
        }

        string? homePath = null;
        if (primary is not null)
        {
            homePath = await db.OrgUnits
                .Where(u => u.Id == primary.OrgUnitId)
                .Select(u => u.Path)
                .FirstOrDefaultAsync(ct);
        }

        var position = primary?.PositionLevel ?? PositionLevel.Employee;
        if (led is not null)
        {
            position = led.Type switch
            {
                OrgUnitType.Team => PositionLevel.TeamLeader,
                OrgUnitType.Group => PositionLevel.Manager,
                OrgUnitType.Directorate => PositionLevel.DCEO,
                OrgUnitType.Company => PositionLevel.CEO,
                _ => position,
            };
        }

        return new OrgProfile(userId, position, primary?.OrgUnitId, homePath, led?.Id);
    }

    public async Task<Guid?> ResolveScopeUnitAsync(string userId, VisibilityScope scope, CancellationToken ct = default)
    {
        if (scope == VisibilityScope.Company)
        {
            return null; // Company scope = all KOC; no specific unit.
        }

        var targetType = scope switch
        {
            VisibilityScope.Team => OrgUnitType.Team,
            VisibilityScope.Group => OrgUnitType.Group,
            VisibilityScope.Directorate => OrgUnitType.Directorate,
            _ => OrgUnitType.Company,
        };

        var homePath = await db.OrgMemberships
            .Where(m => m.UserId == userId && m.IsPrimary && m.ToUtc == null)
            .Join(db.OrgUnits, m => m.OrgUnitId, u => u.Id, (_, u) => u.Path)
            .FirstOrDefaultAsync(ct);

        if (homePath is null)
        {
            return null;
        }

        // The ancestor-or-self of the home unit whose type matches the requested level.
        return await db.OrgUnits
            .Where(u => u.Type == targetType && (homePath == u.Path || homePath.StartsWith(u.Path + "/")))
            .Select(u => (Guid?)u.Id)
            .FirstOrDefaultAsync(ct);
    }
}
