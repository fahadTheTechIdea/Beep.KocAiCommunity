namespace Beep.KocAiCommunity.Contracts.Admin;

/// <summary>A user as seen in the admin RBAC console: identity, org placement, and creation rights.</summary>
public sealed record AdminUserDto(
    string UserId,
    string? Email,
    string? DisplayName,
    string? CompanyId,
    string? DepartmentId,
    string? DepartmentName,
    string PositionLevel,
    string? MaxCompetitionScope,
    /// <summary>The platform roles this user holds. Assigned here whatever the sign-in method, because
    /// authorization is the app's own concern — the identity provider only establishes who someone is.</summary>
    IReadOnlyList<string>? Roles = null);

/// <summary>
/// Sets a user's identity/org fields. <paramref name="DepartmentCode"/> is an OrgUnit code; the
/// service derives the CompanyId (company-root code) from it so the two codes never drift.
/// </summary>
public sealed record UpsertUserProfileRequest(string? Email, string? DisplayName, string? DepartmentCode);

/// <summary>Grants (or updates) a user's competition-creation capability at a maximum audience level.</summary>
public sealed record SetCompetitionGrantRequest(string MaxScope);

/// <summary>Replaces a user's platform roles with exactly this set.</summary>
public sealed record SetUserRolesRequest(IReadOnlyList<string> Roles);

/// <summary>The role names an administrator may assign, for the console's editor.</summary>
public sealed record AssignableRolesDto(IReadOnlyList<string> Positions, IReadOnlyList<string> Functions);

/// <summary>A learning track and the competition it points people at, for the admin linking editor.</summary>
public sealed record LearningLinkDto(Guid TrackId, string Title, Guid? RecommendedCompetitionId);

/// <summary>Points a learning track at a competition, or clears it when null.</summary>
public sealed record SetRecommendedCompetitionRequest(Guid? CompetitionId);

/// <summary>Points a competition at a learning track, or clears it when null.</summary>
public sealed record SetRecommendedTrackRequest(Guid? TrackId);

/// <summary>An org unit and its assignable business code, for the code editor.</summary>
public sealed record OrgUnitCodeDto(Guid Id, string Name, string Type, string Path, string? Code);

/// <summary>Assigns (or renames) an org unit's business code, e.g. "AX01".</summary>
public sealed record SetOrgUnitCodeRequest(string? Code);
