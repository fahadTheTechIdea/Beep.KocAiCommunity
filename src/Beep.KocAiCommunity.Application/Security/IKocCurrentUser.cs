using Beep.KocAiCommunity.Domain.Organization;

namespace Beep.KocAiCommunity.Application.Security;

/// <summary>The signed-in KOC user, resolved from Entra claims and the org directory.</summary>
public interface IKocCurrentUser
{
    bool IsAuthenticated { get; }
    string? UserId { get; }
    string? DisplayName { get; }
    IReadOnlyCollection<string> Roles { get; }
    PositionLevel PositionLevel { get; }

    /// <summary>The user's home Team org unit id (their primary membership).</summary>
    Guid? HomeOrgUnitId { get; }

    /// <summary>For leaders, the org unit they lead (Team/Group/Directorate/Company); otherwise null.</summary>
    Guid? LedOrgUnitId { get; }

    bool IsInRole(string role);
}
