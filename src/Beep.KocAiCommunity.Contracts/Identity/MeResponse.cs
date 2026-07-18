namespace Beep.KocAiCommunity.Contracts.Identity;

/// <summary>The signed-in user's identity, roles, and place in the KOC org tree.</summary>
public sealed record MeResponse(
    string UserId,
    string? DisplayName,
    IReadOnlyList<string> Roles,
    string PositionLevel,
    Guid? HomeOrgUnitId,
    Guid? LedOrgUnitId);
