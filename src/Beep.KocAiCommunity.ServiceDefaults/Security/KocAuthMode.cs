namespace Beep.KocAiCommunity.ServiceDefaults.Security;

/// <summary>
/// Where people prove who they are. This is the <b>only</b> question the first run asks, because it is
/// the only one that cannot be answered from inside a site nobody can sign in to yet. Everything else —
/// which corporate mechanism, the Entra tenant, and every other setting — is managed in the site's admin
/// settings at runtime.
/// </summary>
public enum KocSignInSource
{
    /// <summary>Nothing chosen yet — the app sends every visitor to the setup wizard.</summary>
    Unconfigured = 0,

    /// <summary>
    /// The KOC environment: the corporate Windows account the visitor already holds, verified by IIS
    /// before a request reaches this app. Nothing to configure — IIS is the configuration.
    /// </summary>
    KocEnvironment = 1,

    /// <summary>Accounts belonging to this site: people register and sign in with a password here.</summary>
    SiteAccounts = 2,
}

/// <summary>Display text for the one first-run choice, so the wizard doesn't hardcode prose.</summary>
public sealed record KocSignInSourceInfo(KocSignInSource Source, string DisplayName, string Summary)
{
    public static readonly IReadOnlyList<KocSignInSourceInfo> All =
    [
        new(KocSignInSource.KocEnvironment, "The KOC environment",
            "People arrive already signed in with their corporate Windows account — IIS verifies them before the request reaches this site, and there is no password here to hold or configure. Choose this for a deployment inside KOC."),
        new(KocSignInSource.SiteAccounts, "Accounts on this site",
            "People register with an email and password here. Choose this when the site is published on the web, outside the corporate network."),
    ];

    public static KocSignInSourceInfo? Find(KocSignInSource source) => All.FirstOrDefault(s => s.Source == source);
}
