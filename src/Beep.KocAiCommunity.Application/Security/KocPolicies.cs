namespace Beep.KocAiCommunity.Application.Security;

/// <summary>Authorization policy names registered in the hosts.</summary>
public static class KocPolicies
{
    public const string RequireEmployee = "RequireEmployee";
    public const string RequirePlatformAdmin = "RequirePlatformAdmin";
    public const string RequireCompetitionAdmin = "RequireCompetitionAdmin";
    public const string RequireLearningAdmin = "RequireLearningAdmin";
    public const string RequireAuditor = "RequireAuditor";
    public const string RequireSupervisor = "RequireSupervisor";
}
