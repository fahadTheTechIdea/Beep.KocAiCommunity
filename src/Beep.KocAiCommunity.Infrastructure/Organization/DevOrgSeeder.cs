using Beep.KocAiCommunity.Domain.Authorization;
using Beep.KocAiCommunity.Domain.Engagement;
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
            await EnsureAdminMembershipAsync(db, ct);   // self-heal databases seeded before dev-admin existed
            return;
        }

        var stamp = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var company = Unit("Kuwait Oil Company", OrgUnitType.Company, null, "/koc", "KOC", "dev-ceo", stamp);
        var directorate = Unit("Exploration & Subsurface", OrgUnitType.Directorate, company.Id, "/koc/exploration", "EXP", "dev-dceo", stamp);
        var group = Unit("Subsurface", OrgUnitType.Group, directorate.Id, "/koc/exploration/subsurface", "SUB", "dev-user", stamp);
        var reservoir = Unit("Reservoir Analytics", OrgUnitType.Team, group.Id, "/koc/exploration/subsurface/reservoir", "AX01", "dev-lead", stamp);
        var production = Unit("Production Engineering", OrgUnitType.Team, group.Id, "/koc/exploration/subsurface/production", "AX02", null, stamp);

        db.OrgUnits.AddRange(company, directorate, group, reservoir, production);
        db.OrgMemberships.AddRange(
            Member("dev-user", group.Id, PositionLevel.Manager, stamp),
            Member("dev-admin", group.Id, PositionLevel.Manager, stamp),   // the Platform Admin persona
            Member("dev-lead", reservoir.Id, PositionLevel.TeamLeader, stamp),
            Member("dev-emp-1", reservoir.Id, PositionLevel.Employee, stamp),
            Member("dev-emp-2", reservoir.Id, PositionLevel.Employee, stamp),
            Member("dev-emp-3", production.Id, PositionLevel.Employee, stamp));

        // Identity/org profiles (email + company/dept codes + the authoritative OrgUnitId) so the
        // admin RBAC console has real users to manage.
        db.UserProfiles.AddRange(
            Profile("dev-ceo", "Nasser Al-Sabah", company, stamp),
            Profile("dev-dceo", "Huda Al-Fadhli", directorate, stamp),
            Profile("dev-user", "Yousef Al-Mutairi", group, stamp),
            Profile("dev-admin", "Platform Admin", group, stamp),
            Profile("dev-compadmin", "Competition Admin", group, stamp),
            Profile("dev-lead", "Sara Al-Rashidi", reservoir, stamp),
            Profile("dev-emp-1", "Ali Al-Ajmi", reservoir, stamp),
            Profile("dev-emp-2", "Mariam Al-Enezi", reservoir, stamp),
            Profile("dev-emp-3", "Khaled Al-Dosari", production, stamp));

        // Competition-creator grants so the non-admin personas can still host, capped to their level.
        // The Platform Admin persona (dev-admin) needs none — it may always create at any level.
        db.CompetitionCreatorGrants.AddRange(
            Grant("dev-lead", VisibilityScope.Team, stamp),
            Grant("dev-user", VisibilityScope.Group, stamp),
            Grant("dev-dceo", VisibilityScope.Directorate, stamp),
            Grant("dev-ceo", VisibilityScope.Company, stamp),
            Grant("dev-compadmin", VisibilityScope.Company, stamp));
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

    // Databases seeded before the dev-admin persona existed lack its membership; add it once.
    private static async Task EnsureAdminMembershipAsync(KocDbContext db, CancellationToken ct)
    {
        if (await db.OrgMemberships.AnyAsync(m => m.UserId == "dev-admin", ct))
        {
            return;
        }

        var groupId = await db.OrgUnits.Where(u => u.Path == "/koc/exploration/subsurface").Select(u => (Guid?)u.Id).FirstOrDefaultAsync(ct);
        if (groupId is { } id)
        {
            db.OrgMemberships.Add(Member("dev-admin", id, PositionLevel.Manager, DateTime.UtcNow));
            await db.SaveChangesAsync(ct);
        }
    }

    private static OrgUnit Unit(string name, OrgUnitType type, Guid? parent, string path, string code, string? leader, DateTime stamp) => new()
    {
        Name = name,
        Type = type,
        ParentId = parent,
        Path = path,
        Code = code,
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

    private static UserProfile Profile(string userId, string displayName, OrgUnit unit, DateTime stamp) => new()
    {
        UserId = userId,
        DisplayName = displayName,
        Email = $"{userId}@koc.com.kw",
        CompanyId = "KOC",
        DepartmentId = unit.Code,
        OrgUnitId = unit.Id,
        CreatedUtc = stamp,
    };

    private static CompetitionCreatorGrant Grant(string userId, VisibilityScope maxScope, DateTime stamp) => new()
    {
        UserId = userId,
        MaxScope = maxScope,
        GrantedByUserId = "dev-seed",
        CreatedUtc = stamp,
    };
}
