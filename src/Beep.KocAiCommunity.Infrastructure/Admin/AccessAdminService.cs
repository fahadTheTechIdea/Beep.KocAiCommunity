using Beep.KocAiCommunity.Application.Admin;
using Beep.KocAiCommunity.Application.Audit;
using Beep.KocAiCommunity.Application.Security;
using Beep.KocAiCommunity.Domain.Authorization;
using Beep.KocAiCommunity.Domain.Engagement;
using Beep.KocAiCommunity.Domain.Organization;
using Beep.KocAiCommunity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Beep.KocAiCommunity.Infrastructure.Admin;

/// <summary>Platform-admin RBAC management over user profiles, competition-creator grants, and org codes.</summary>
public sealed class AccessAdminService(KocDbContext db, IAuditEnvelope audit, IKocCurrentUser me, IKocUserDirectory directory) : IAccessAdminService
{
    public async Task<IReadOnlyList<AccessUserView>> ListUsersAsync(CancellationToken ct = default)
    {
        var profiles = await db.UserProfiles.AsNoTracking().ToDictionaryAsync(p => p.UserId, ct);
        var memberByUser = (await db.OrgMemberships.AsNoTracking()
                .Where(m => m.IsPrimary && m.ToUtc == null).ToListAsync(ct))
            .GroupBy(m => m.UserId).ToDictionary(g => g.Key, g => g.First());
        var grants = await db.CompetitionCreatorGrants.AsNoTracking().ToDictionaryAsync(g => g.UserId, ct);

        // Resolve a department code → unit name for display. Codes are unique.
        var nameByCode = await db.OrgUnits.AsNoTracking().Where(u => u.Code != null)
            .ToDictionaryAsync(u => u.Code!, u => u.Name, ct);

        // Anyone the platform has recorded — including a colleague who has only ever signed in through
        // the corporate network — belongs in the console, so their roles can be managed here.
        var rolesByUser = await db.Set<Microsoft.AspNetCore.Identity.IdentityUserRole<string>>().AsNoTracking()
            .Join(db.Set<Microsoft.AspNetCore.Identity.IdentityRole>().AsNoTracking(),
                ur => ur.RoleId, r => r.Id, (ur, r) => new { ur.UserId, r.Name })
            .ToListAsync(ct);
        var roleNames = rolesByUser
            .GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)[.. g.Select(x => x.Name!).OrderBy(n => n, StringComparer.Ordinal)]);
        var knownUserIds = await db.Set<Microsoft.AspNetCore.Identity.IdentityUser>().AsNoTracking()
            .Select(u => u.Id).ToListAsync(ct);

        var userIds = profiles.Keys
            .Union(memberByUser.Keys)
            .Union(grants.Keys)
            .Union(knownUserIds)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();

        var views = new List<AccessUserView>(userIds.Count);
        foreach (var userId in userIds)
        {
            profiles.TryGetValue(userId, out var profile);
            memberByUser.TryGetValue(userId, out var membership);
            var position = membership?.PositionLevel ?? PositionLevel.Employee;
            var departmentName = profile?.DepartmentId is { } code ? nameByCode.GetValueOrDefault(code) : null;
            VisibilityScope? maxScope = grants.TryGetValue(userId, out var grant) && grant.IsActive(DateTime.UtcNow)
                ? grant.MaxScope
                : null;

            views.Add(new AccessUserView(
                userId, profile?.Email, profile?.DisplayName, profile?.CompanyId, profile?.DepartmentId,
                departmentName, position, maxScope, roleNames.GetValueOrDefault(userId, [])));
        }

        return views;
    }

    public async Task<IReadOnlyList<OrgUnitCodeView>> ListOrgUnitsAsync(CancellationToken ct = default) =>
        await db.OrgUnits.AsNoTracking().OrderBy(u => u.Path)
            .Select(u => new OrgUnitCodeView(u.Id, u.Name, u.Type, u.Path, u.Code))
            .ToListAsync(ct);

    public async Task<AccessUserView> UpsertProfileAsync(string userId, string? email, string? displayName, string? departmentCode, CancellationToken ct = default)
    {
        email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
        if (email is not null && await db.UserProfiles.AnyAsync(p => p.Email == email && p.UserId != userId, ct))
        {
            throw new AccessAdminException($"Email '{email}' is already used by another user.");
        }

        departmentCode = string.IsNullOrWhiteSpace(departmentCode) ? null : departmentCode.Trim();
        OrgUnit? unit = null;
        string? companyCode = null;
        string? departmentName = null;
        if (departmentCode is not null)
        {
            unit = await db.OrgUnits.FirstOrDefaultAsync(u => u.Code == departmentCode, ct)
                ?? throw new AccessAdminException($"No org unit has the code '{departmentCode}'.");
            departmentName = unit.Name;

            // Company-root code = the Company-type ancestor on the unit's materialized path.
            companyCode = await db.OrgUnits
                .Where(u => u.Type == OrgUnitType.Company && (unit.Path == u.Path || unit.Path.StartsWith(u.Path + "/")))
                .Select(u => u.Code)
                .FirstOrDefaultAsync(ct);
        }

        var profile = await db.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId, ct);
        if (profile is null)
        {
            profile = new UserProfile
            {
                UserId = userId,
                DisplayName = string.IsNullOrWhiteSpace(displayName) ? userId : displayName.Trim(),
                CreatedUtc = DateTime.UtcNow,
            };
            db.UserProfiles.Add(profile);
        }
        else if (!string.IsNullOrWhiteSpace(displayName))
        {
            profile.DisplayName = displayName.Trim();
        }

        profile.Email = email;
        profile.DepartmentId = departmentCode;
        profile.CompanyId = companyCode;

        await db.SaveChangesAsync(ct);
        await audit.WriteAsync(new AuditEntry("user-profile.upsert", "user", userId,
            AfterJson: $"{{\"email\":\"{email}\",\"company\":\"{companyCode}\",\"dept\":\"{departmentCode}\"}}"), ct);

        var position = await db.OrgMemberships.AsNoTracking()
            .Where(m => m.UserId == userId && m.IsPrimary && m.ToUtc == null)
            .Select(m => (PositionLevel?)m.PositionLevel).FirstOrDefaultAsync(ct) ?? PositionLevel.Employee;
        var maxScope = await db.CompetitionCreatorGrants.AsNoTracking()
            .Where(g => g.UserId == userId).Select(g => (VisibilityScope?)g.MaxScope).FirstOrDefaultAsync(ct);

        return new AccessUserView(userId, profile.Email, profile.DisplayName, profile.CompanyId, profile.DepartmentId,
            departmentName, position, maxScope);
    }

    public async Task SetUserRolesAsync(string userId, IReadOnlyList<string> roles, CancellationToken ct = default)
    {
        // Losing the last administrator would leave the platform unmanageable with no way back short of
        // a database edit — refuse rather than let an admin remove their own last foothold.
        if (!roles.Contains(KocRoles.PlatformAdmin, StringComparer.OrdinalIgnoreCase)
            && await IsLastPlatformAdminAsync(userId, ct))
        {
            throw new AccessAdminException("This is the only Platform Admin — grant the role to someone else before removing it here.");
        }

        try
        {
            await directory.SetRolesAsync(userId, roles, ct);
        }
        catch (InvalidOperationException ex)
        {
            throw new AccessAdminException(ex.Message);
        }

        await audit.WriteAsync(new AuditEntry("user-roles.set", "user", userId,
            AfterJson: $"{{\"roles\":\"{string.Join(',', roles)}\"}}"), ct);
    }

    public async Task<AccessUserView> SetUserPositionAsync(string userId, PositionLevel position, CancellationToken ct = default)
    {
        var membership = await db.OrgMemberships
            .FirstOrDefaultAsync(m => m.UserId == userId && m.IsPrimary && m.ToUtc == null, ct);

        if (membership is null)
        {
            // Create one from the department already recorded on the profile, so an administrator can do
            // this in the order the console presents it — set the department, then the position.
            var departmentCode = await db.UserProfiles.AsNoTracking()
                .Where(p => p.UserId == userId).Select(p => p.DepartmentId).FirstOrDefaultAsync(ct);

            if (departmentCode is null)
            {
                throw new AccessAdminException(
                    "Give this person a department first — a position level is a place in the reporting line.");
            }

            var unit = await db.OrgUnits.FirstOrDefaultAsync(u => u.Code == departmentCode, ct)
                ?? throw new AccessAdminException($"No org unit has the code '{departmentCode}'.");

            membership = new OrgMembership
            {
                UserId = userId,
                OrgUnitId = unit.Id,
                IsPrimary = true,
                FromUtc = DateTime.UtcNow,
                CreatedByUserId = me.UserId ?? userId,
                CreatedUtc = DateTime.UtcNow,
            };
            db.OrgMemberships.Add(membership);
        }

        membership.PositionLevel = position;
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync(new AuditEntry("user-position.set", "user", userId,
            AfterJson: $"{{\"position\":\"{position}\"}}"), ct);

        return (await ListUsersAsync(ct)).First(u => u.UserId == userId);
    }

    private async Task<bool> IsLastPlatformAdminAsync(string userId, CancellationToken ct)
    {
        var adminRoleId = await db.Set<Microsoft.AspNetCore.Identity.IdentityRole>().AsNoTracking()
            .Where(r => r.Name == KocRoles.PlatformAdmin).Select(r => r.Id).FirstOrDefaultAsync(ct);
        if (adminRoleId is null)
        {
            return false;
        }

        var admins = await db.Set<Microsoft.AspNetCore.Identity.IdentityUserRole<string>>().AsNoTracking()
            .Where(ur => ur.RoleId == adminRoleId).Select(ur => ur.UserId).ToListAsync(ct);
        return admins.Count == 1 && admins[0] == userId;
    }

    public async Task SetCompetitionGrantAsync(string userId, VisibilityScope maxScope, CancellationToken ct = default)
    {
        var grant = await db.CompetitionCreatorGrants.FirstOrDefaultAsync(g => g.UserId == userId, ct);
        if (grant is null)
        {
            grant = new CompetitionCreatorGrant
            {
                UserId = userId,
                MaxScope = maxScope,
                GrantedByUserId = me.UserId ?? "system",
                CreatedUtc = DateTime.UtcNow,
            };
            db.CompetitionCreatorGrants.Add(grant);
        }
        else
        {
            grant.MaxScope = maxScope;
            grant.GrantedByUserId = me.UserId ?? "system";
        }

        await db.SaveChangesAsync(ct);
        await audit.WriteAsync(new AuditEntry("competition-grant.set", "user", userId, AfterJson: maxScope.ToString()), ct);
    }

    public async Task RevokeCompetitionGrantAsync(string userId, CancellationToken ct = default)
    {
        var grant = await db.CompetitionCreatorGrants.FirstOrDefaultAsync(g => g.UserId == userId, ct);
        if (grant is null)
        {
            return;
        }

        db.CompetitionCreatorGrants.Remove(grant);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync(new AuditEntry("competition-grant.revoke", "user", userId, BeforeJson: grant.MaxScope.ToString()), ct);
    }

    public async Task SetOrgUnitCodeAsync(Guid orgUnitId, string? code, CancellationToken ct = default)
    {
        code = string.IsNullOrWhiteSpace(code) ? null : code.Trim();
        var unit = await db.OrgUnits.FirstOrDefaultAsync(u => u.Id == orgUnitId, ct)
            ?? throw new AccessAdminException("Org unit not found.");

        if (code is not null && await db.OrgUnits.AnyAsync(u => u.Code == code && u.Id != orgUnitId, ct))
        {
            throw new AccessAdminException($"Code '{code}' is already assigned to another unit.");
        }

        var before = unit.Code;
        unit.Code = code;
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync(new AuditEntry("org-unit.code", "org-unit", orgUnitId.ToString(), BeforeJson: before, AfterJson: code), ct);
    }
}
