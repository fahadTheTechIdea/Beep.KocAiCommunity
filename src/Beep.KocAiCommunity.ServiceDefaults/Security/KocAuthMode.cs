namespace Beep.KocAiCommunity.ServiceDefaults.Security;

/// <summary>
/// How the platform authenticates people. Chosen once in the first-run wizard and persisted by
/// <see cref="KocSetupStore"/>; both the Web and the API read the same answer so they agree on who a
/// caller is. Authentication schemes are wired at startup, so changing the mode needs a restart.
/// </summary>
public enum KocAuthMode
{
    /// <summary>Nothing chosen yet — the app sends every visitor to the setup wizard.</summary>
    Unconfigured = 0,

    /// <summary>
    /// Accounts held by this app (ASP.NET Core Identity): people register with an email and password
    /// and sign in on a login page. The default for a normal web deployment.
    /// </summary>
    LocalAccounts = 1,

    /// <summary>
    /// Intranet single sign-on (Negotiate/Kerberos). The browser hands the site the signed-in Windows
    /// account and there is no login page — the KOC-network deployment.
    /// </summary>
    WindowsIntranet = 2,

    /// <summary>Microsoft Entra ID (Azure AD) sign-in against the corporate tenant.</summary>
    EntraId = 3,

    /// <summary>
    /// No real sign-in: the app runs as a switchable demo persona. For local development and
    /// demonstrations only — <see cref="KocProductionPreflight"/> refuses to start Production on it.
    /// </summary>
    DemoPersonas = 4,
}

/// <summary>Display metadata for the modes, so the wizard doesn't hardcode prose per option.</summary>
public sealed record KocAuthModeInfo(KocAuthMode Mode, string DisplayName, string Summary, bool NeedsRestartToApply = true)
{
    public static readonly IReadOnlyList<KocAuthModeInfo> All =
    [
        new(KocAuthMode.LocalAccounts, "Accounts on this site",
            "People register with an email and password and sign in here. Choose this to run like an ordinary website."),
        new(KocAuthMode.WindowsIntranet, "Corporate intranet sign-on (Windows)",
            "The browser passes each visitor's signed-in Windows account through automatically — no login page. Requires the site to run on the corporate network with Windows Authentication enabled."),
        new(KocAuthMode.EntraId, "Microsoft Entra ID",
            "Sign in against the corporate Entra (Azure AD) tenant. Needs a tenant id and an app registration."),
        new(KocAuthMode.DemoPersonas, "Demo — no sign-in",
            "Skip authentication entirely and switch between sample roles from the top bar. For local exploration only; the app refuses to run this way in Production."),
    ];

    public static KocAuthModeInfo? Find(KocAuthMode mode) => All.FirstOrDefault(m => m.Mode == mode);
}
