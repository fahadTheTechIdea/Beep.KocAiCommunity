using Beep.KocAiCommunity.Domain.Organization;

namespace Beep.KocAiCommunity.Application.Admin;

/// <summary>Raised when an RBAC admin action is invalid (unknown unit/user, duplicate code/email).</summary>
public sealed class AccessAdminException(string message) : Exception(message);

/// <summary>A user row for the admin RBAC console.</summary>
public sealed record AccessUserView(
    string UserId, string? Email, string? DisplayName, string? CompanyId, string? DepartmentId,
    string? DepartmentName, PositionLevel PositionLevel, VisibilityScope? MaxCompetitionScope,
    IReadOnlyList<string>? Roles = null);

/// <summary>An org unit + its business code.</summary>
public sealed record OrgUnitCodeView(Guid Id, string Name, OrgUnitType Type, string Path, string? Code);

/// <summary>
/// Platform-admin RBAC management: who exists, their org identity, their competition-creation
/// rights, and org-unit business codes. Every mutating call is written to the admin audit log.
/// </summary>
public interface IAccessAdminService
{
    /// <summary>Every known user (org members ∪ profiles) with identity, org placement, and creation rights.</summary>
    Task<IReadOnlyList<AccessUserView>> ListUsersAsync(CancellationToken ct = default);

    /// <summary>All org units with their (optional) codes, for the code editor and department pickers.</summary>
    Task<IReadOnlyList<OrgUnitCodeView>> ListOrgUnitsAsync(CancellationToken ct = default);

    /// <summary>
    /// Sets a user's email/display name and, when a department code is given, their org placement —
    /// writing <c>DepartmentId</c> (the unit's code) and <c>CompanyId</c> (the company-root code)
    /// together. A null/blank department code clears the placement. Creates the profile if none.
    /// </summary>
    Task<AccessUserView> UpsertProfileAsync(string userId, string? email, string? displayName, string? departmentCode, CancellationToken ct = default);

    /// <summary>
    /// Replaces a user's platform roles. These decide what every authorization policy allows, and they
    /// live in this database whichever way the person signs in — so an administrator can grant a
    /// corporate-intranet colleague CompetitionAdmin here without touching the directory.
    /// </summary>
    Task SetUserRolesAsync(string userId, IReadOnlyList<string> roles, CancellationToken ct = default);

    /// <summary>Grants (or updates) a user's competition-creation capability at the given max scope.</summary>
    Task SetCompetitionGrantAsync(string userId, VisibilityScope maxScope, CancellationToken ct = default);

    /// <summary>Removes a user's competition-creation capability.</summary>
    Task RevokeCompetitionGrantAsync(string userId, CancellationToken ct = default);

    /// <summary>Assigns (or clears/renames) an org unit's unique business code.</summary>
    Task SetOrgUnitCodeAsync(Guid orgUnitId, string? code, CancellationToken ct = default);
}
