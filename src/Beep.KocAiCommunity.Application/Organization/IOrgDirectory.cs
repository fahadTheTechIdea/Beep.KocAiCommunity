using Beep.KocAiCommunity.Domain.Organization;

namespace Beep.KocAiCommunity.Application.Organization;

/// <summary>A person's resolved place in the org tree.</summary>
public sealed record OrgProfile(
    string UserId,
    PositionLevel PositionLevel,
    Guid? HomeOrgUnitId,
    string? HomeOrgUnitPath,
    Guid? LedOrgUnitId);

/// <summary>Resolves people to their home unit, position, and (for leaders) the unit they lead.</summary>
public interface IOrgDirectory
{
    Task<OrgProfile?> GetProfileAsync(string userId, CancellationToken ct = default);

    /// <summary>
    /// The org unit that a given visibility scope resolves to for a user — the ancestor-or-self of
    /// their home unit at that level. Returns null for <see cref="VisibilityScope.Company"/> (all KOC)
    /// or when the user has no unit at that level.
    /// </summary>
    Task<Guid?> ResolveScopeUnitAsync(string userId, VisibilityScope scope, CancellationToken ct = default);
}
