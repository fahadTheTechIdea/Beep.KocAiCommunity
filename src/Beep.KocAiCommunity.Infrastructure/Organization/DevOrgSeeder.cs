using Beep.KocAiCommunity.Domain.Learning;
using Beep.KocAiCommunity.Domain.Organization;
using Beep.KocAiCommunity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Beep.KocAiCommunity.Infrastructure.Organization;

/// <summary>
/// Development-only seed: a small KOC org tree where <c>dev-user</c> is a Manager over a group,
/// plus a few members and sample learning activity — so the supervision dashboard has data.
/// Runs only in dev startup and only when the org tree is empty.
/// </summary>
public static class DevOrgSeeder
{
    public static async Task SeedDevOrgAsync(KocDbContext db, CancellationToken ct = default)
    {
        if (await db.OrgUnits.AnyAsync(ct))
        {
            return;
        }

        var stamp = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var company = Unit("Kuwait Oil Company", OrgUnitType.Company, null, "/koc", "dev-ceo", stamp);
        var directorate = Unit("Exploration & Subsurface", OrgUnitType.Directorate, company.Id, "/koc/exploration", "dev-dceo", stamp);
        var group = Unit("Subsurface", OrgUnitType.Group, directorate.Id, "/koc/exploration/subsurface", "dev-user", stamp);
        var reservoir = Unit("Reservoir Analytics", OrgUnitType.Team, group.Id, "/koc/exploration/subsurface/reservoir", "dev-lead", stamp);
        var production = Unit("Production Engineering", OrgUnitType.Team, group.Id, "/koc/exploration/subsurface/production", null, stamp);

        db.OrgUnits.AddRange(company, directorate, group, reservoir, production);
        db.OrgMemberships.AddRange(
            Member("dev-user", group.Id, PositionLevel.Manager, stamp),
            Member("dev-lead", reservoir.Id, PositionLevel.TeamLeader, stamp),
            Member("dev-emp-1", reservoir.Id, PositionLevel.Employee, stamp),
            Member("dev-emp-2", reservoir.Id, PositionLevel.Employee, stamp),
            Member("dev-emp-3", production.Id, PositionLevel.Employee, stamp));
        await db.SaveChangesAsync(ct);

        // Sample participation so the rollup isn't empty.
        var trackId = await db.LearningTracks.OrderBy(t => t.OrderNo).Select(t => t.Id).FirstOrDefaultAsync(ct);
        if (trackId != Guid.Empty)
        {
            db.TrackEnrollments.Add(new TrackEnrollment { TrackId = trackId, UserId = "dev-emp-1", Status = "completed", StartedUtc = stamp, CompletedUtc = stamp, CreatedUtc = stamp });
            db.TrackCompletions.Add(new TrackCompletion { TrackId = trackId, UserId = "dev-emp-1", CompletedUtc = stamp, CreatedUtc = stamp });
            db.TrackEnrollments.Add(new TrackEnrollment { TrackId = trackId, UserId = "dev-emp-2", Status = "active", StartedUtc = stamp, CreatedUtc = stamp });
            await db.SaveChangesAsync(ct);
        }
    }

    private static OrgUnit Unit(string name, OrgUnitType type, Guid? parent, string path, string? leader, DateTime stamp) => new()
    {
        Name = name,
        Type = type,
        ParentId = parent,
        Path = path,
        LeaderUserId = leader,
        CreatedUtc = stamp,
    };

    private static OrgMembership Member(string userId, Guid orgUnitId, PositionLevel level, DateTime stamp) => new()
    {
        UserId = userId,
        OrgUnitId = orgUnitId,
        PositionLevel = level,
        IsPrimary = true,
        FromUtc = stamp,
        CreatedUtc = stamp,
    };
}
