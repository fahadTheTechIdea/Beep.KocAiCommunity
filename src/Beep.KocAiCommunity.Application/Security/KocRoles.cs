namespace Beep.KocAiCommunity.Application.Security;

/// <summary>
/// Canonical role names. Position levels mirror the KOC reporting line (one per user, from the
/// org directory); function roles are additive platform capabilities granted by an admin.
/// </summary>
public static class KocRoles
{
    // Position levels
    public const string Employee = "Employee";
    public const string TeamLeader = "TeamLeader";
    public const string Manager = "Manager";
    public const string DCEO = "DCEO";
    public const string CEO = "CEO";

    // Function roles
    public const string PlatformAdmin = "PlatformAdmin";
    public const string CompetitionAdmin = "CompetitionAdmin";
    public const string LearningAdmin = "LearningAdmin";
    public const string Auditor = "Auditor";

    /// <summary>Any position level counts as a participant ("at least Employee").</summary>
    public static readonly string[] AllPositions = [Employee, TeamLeader, Manager, DCEO, CEO];

    /// <summary>Position levels that supervise a subtree.</summary>
    public static readonly string[] SupervisorPositions = [TeamLeader, Manager, DCEO, CEO];
}
