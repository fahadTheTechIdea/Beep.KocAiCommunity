namespace Beep.KocAiCommunity.Contracts.Identity;

/// <summary>The signed-in user's identity, roles, and place in the KOC org tree.</summary>
public sealed record MeResponse(
    string UserId,
    string? DisplayName,
    IReadOnlyList<string> Roles,
    string PositionLevel,
    Guid? HomeOrgUnitId,
    Guid? LedOrgUnitId,
    /// <summary>Widest competition audience this user may create (Team/Group/Directorate/Company), or null if they cannot create competitions.</summary>
    string? MaxCompetitionScope = null);
