namespace Beep.KocAiCommunity.Contracts.Admin;

/// <summary>A user as seen in the admin RBAC console: identity, org placement, and creation rights.</summary>
public sealed record AdminUserDto(
    string UserId,
    string? Email,
    string? DisplayName,
    string? CompanyId,
    string? DepartmentId,
    Guid? OrgUnitId,
    string? OrgUnitCode,
    string? OrgUnitName,
    string PositionLevel,
    string? MaxCompetitionScope);

/// <summary>Sets a user's identity/org fields. Picking an org unit sets the codes together so they never drift.</summary>
public sealed record UpsertUserProfileRequest(string? Email, string? DisplayName, Guid? OrgUnitId);

/// <summary>Grants (or updates) a user's competition-creation capability at a maximum audience level.</summary>
public sealed record SetCompetitionGrantRequest(string MaxScope);

/// <summary>An org unit and its assignable business code, for the code editor.</summary>
public sealed record OrgUnitCodeDto(Guid Id, string Name, string Type, string Path, string? Code);

/// <summary>Assigns (or renames) an org unit's business code, e.g. "AX01".</summary>
public sealed record SetOrgUnitCodeRequest(string? Code);
