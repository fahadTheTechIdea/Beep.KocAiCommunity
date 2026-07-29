using Beep.KocAiCommunity.ServiceDefaults.Security;
using Beep.KocAiCommunity.Ui.Shared.Localization;
using Microsoft.Extensions.Localization;

namespace Beep.KocAiCommunity.Web.Security;

/// <summary>
/// What a "sign in" invitation should say and do, given where this installation signs people in.
/// <para>
/// One place, because the landing page alone asks four times. Hardcoding "Sign in with KOC" is wrong on
/// a site published to the web, where there is no KOC account to use — and sending people to a login
/// page is wrong inside KOC, where IIS has already signed them in and no such page exists.
/// </para>
/// </summary>
public static class SignInPrompt
{
    /// <summary>True when this site has a login page of its own to send people to.</summary>
    public static bool UsesLoginPage(KocSetupStore setup) =>
        setup.SignInWith == KocSignInSource.SiteAccounts && !setup.DemoPersonasEnabled;

    /// <summary>Every phrase this class can produce, for the localization coverage test.</summary>
    public static readonly string[] Translatable =
    [
        "Sign in", "Sign in with KOC", "Sign in to {0}", "Sign in with KOC to {0}",
        "Use the persona picker in the top right to view the app as different roles.",
        "Sign in with your account, or register if you don't have one yet.",
        "You are signed in automatically with your KOC account — if you are seeing this, contact the platform team.",
    ];

    /// <summary>The call to action on a button.</summary>
    public static string ButtonText(KocSetupStore setup, IStringLocalizer<Strings> l) =>
        UsesLoginPage(setup) ? l["Sign in"] : l["Sign in with KOC"];

    /// <summary>The same invitation phrased as "sign in to do X".</summary>
    public static string ButtonText(KocSetupStore setup, IStringLocalizer<Strings> l, string toDoWhat) =>
        UsesLoginPage(setup) ? l["Sign in to {0}", toDoWhat] : l["Sign in with KOC to {0}", toDoWhat];

    /// <summary>Where the button goes, or null when a handler deals with it (demo personas).</summary>
    public static string? Href(KocSetupStore setup) => UsesLoginPage(setup) ? "/account/login" : null;

    /// <summary>How someone signs in here, for explanatory copy on a gate or an empty state.</summary>
    public static string HowToSignIn(KocSetupStore setup, IStringLocalizer<Strings> l) => setup switch
    {
        { DemoPersonasEnabled: true } => l["Use the persona picker in the top right to view the app as different roles."],
        { SignInWith: KocSignInSource.SiteAccounts } => l["Sign in with your account, or register if you don't have one yet."],
        _ => l["You are signed in automatically with your KOC account — if you are seeing this, contact the platform team."],
    };
}
