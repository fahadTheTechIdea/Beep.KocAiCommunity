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
public sealed class AccessAdminService(KocDbContext db, IAuditEnvelope audit, IKocCurrentUser me) : IAccessAdminService
{
    public async Task<IReadOnlyList<AccessUserView>> ListUsersAsync(CancellationToken ct = default)
    {
        var profiles = await db.UserProfiles.AsNoTracking().ToDictionaryAsync(p => p.UserId, ct);
        var memberships = await db.OrgMemberships.AsNoTracking()
            .Where(m => m.IsPrimary && m.ToUtc == null).ToListAsync(ct);
        var memberByUser = memberships
            .GroupBy(m => m.UserId).ToDictionary(g => g.Key, g => g.First());
        var grants = await db.CompetitionCreatorGrants.AsNoTracking().ToDictionaryAsync(g => g.UserId, ct);
        var units = await db.OrgUnits.AsNoTracking().ToDictionaryAsync(u => u.Id, ct);

        var userIds = profiles.Keys
            .Union(memberByUser.Keys)
            .Union(grants.Keys)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();

        var views = new List<AccessUserView>(userIds.Count);
        foreach (var userId in userIds)
        {
            profiles.TryGetValue(userId, out var profile);
            memberByUser.TryGetValue(userId, out var membership);
            var orgUnitId = profile?.OrgUnitId ?? membership?.OrgUnitId;
            OrgUnit? unit = orgUnitId is { } uid && units.TryGetValue(uid, out var u) ? u : null;
            var position = membership?.PositionLevel ?? PositionLevel.Employee;
            VisibilityScope? maxScope = grants.TryGetValue(userId, out var grant) && grant.IsActive(DateTime.UtcNow)
                ? grant.MaxScope
                : null;

            views.Add(new AccessUserView(
                userId, profile?.Email, profile?.DisplayName, profile?.CompanyId, profile?.DepartmentId,
                orgUnitId, unit?.Code, unit?.Name, position, maxScope));
        }

        return views;
    }

    public async Task<IReadOnlyList<OrgUnitCodeView>> ListOrgUnitsAsync(CancellationToken ct = default) =>
        await db.OrgUnits.AsNoTracking().OrderBy(u => u.Path)
            .Select(u => new OrgUnitCodeView(u.Id, u.Name, u.Type, u.Path, u.Code))
            .ToListAsync(ct);

    public async Task<AccessUserView> UpsertProfileAsync(string userId, string? email, string? displayName, Guid? orgUnitId, CancellationToken ct = default)
    {
        email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
        if (email is not null && await db.UserProfiles.AnyAsync(p => p.Email == email && p.UserId != userId, ct))
        {
            throw new AccessAdminException($"Email '{email}' is already used by another user.");
        }

        OrgUnit? unit = null;
        if (orgUnitId is { } uid)
        {
            unit = await db.OrgUnits.FirstOrDefaultAsync(u => u.Id == uid, ct)
                ?? throw new AccessAdminException("Org unit not found.");
        }

        // Company-root code = the Company-type ancestor on the unit's materialized path.
        string? companyCode = null;
        if (unit is not null)
        {
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
        if (unit is not null)
        {
            profile.OrgUnitId = unit.Id;
            profile.DepartmentId = unit.Code;
            profile.CompanyId = companyCode;
        }

        await db.SaveChangesAsync(ct);
        await audit.WriteAsync(new AuditEntry("user-profile.upsert", "user", userId,
            AfterJson: $"{{\"email\":\"{email}\",\"orgUnitId\":\"{unit?.Id}\",\"dept\":\"{profile.DepartmentId}\"}}"), ct);

        var position = await db.OrgMemberships.AsNoTracking()
            .Where(m => m.UserId == userId && m.IsPrimary && m.ToUtc == null)
            .Select(m => (PositionLevel?)m.PositionLevel).FirstOrDefaultAsync(ct) ?? PositionLevel.Employee;
        var maxScope = await db.CompetitionCreatorGrants.AsNoTracking()
            .Where(g => g.UserId == userId).Select(g => (VisibilityScope?)g.MaxScope).FirstOrDefaultAsync(ct);

        return new AccessUserView(userId, profile.Email, profile.DisplayName, profile.CompanyId, profile.DepartmentId,
            profile.OrgUnitId, unit?.Code, unit?.Name, position, maxScope);
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
