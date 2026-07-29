using System.Globalization;
using Beep.KocAiCommunity.Client;
using Beep.KocAiCommunity.Contracts.Localization;
using Microsoft.AspNetCore.Localization;

namespace Beep.KocAiCommunity.Web.Security;

/// <summary>
/// Which language the interface speaks, and how that choice is remembered.
/// <para>
/// The cookie is the primary store rather than the user's profile, because the two pages that most
/// need Arabic — the learning catalogue and the community — are open to people with no account at all.
/// A signed-in member's profile then carries the choice to their other devices; sign-in copies it into
/// the cookie, and switching writes both.
/// </para>
/// </summary>
public static class KocLocalization
{
    /// <summary>Where the switcher sends the visitor. Outside the circuit, because a circuit cannot set a cookie.</summary>
    public const string SetCulturePath = "/culture/set";

    public static IServiceCollection AddKocLocalization(this IServiceCollection services)
    {
        services.AddLocalization();
        services.Configure<RequestLocalizationOptions>(options =>
        {
            // Only the words change with the language. Numbers and dates are pinned to one culture in
            // both, so a leaderboard score reads identically to two colleagues comparing screenshots.
            var formatting = new CultureInfo(KocLanguages.FormattingCulture);
            options.DefaultRequestCulture = new RequestCulture(formatting, new CultureInfo(KocLanguages.English));
            options.SupportedCultures = [formatting];
            options.SupportedUICultures = [.. KocLanguages.All.Select(l => new CultureInfo(l))];

            // Our cookie, our format — the framework's own CookieRequestCultureProvider expects a
            // "c=..|uic=.." payload, which is more than a two-letter choice needs.
            options.RequestCultureProviders =
            [
                new CustomRequestCultureProvider(context =>
                {
                    var cookie = context.Request.Cookies[KocLanguages.CookieName];
                    return Task.FromResult<ProviderCultureResult?>(
                        new ProviderCultureResult(KocLanguages.FormattingCulture, KocLanguages.Normalize(cookie)));
                }),
                new AcceptLanguageHeaderRequestCultureProvider(),
            ];
        });

        return services;
    }

    /// <summary>
    /// Writes the cookie and sends the visitor back where they were. A full reload is the point — the
    /// culture has to be in force before the circuit starts.
    /// <para>
    /// A GET, not a POST: a menu item cannot submit a form, and the whole effect of a forged request
    /// here is that someone sees the site in the other language and clicks the switcher back.
    /// </para>
    /// </summary>
    public static IEndpointRouteBuilder MapKocCultureEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(SetCulturePath, async (HttpContext http, IKocApiClient api, string language, string? returnUrl) =>
        {
            var chosen = KocLanguages.Normalize(language);

            // The cookie is what every request reads, so it is written first and unconditionally —
            // a visitor with no account gets the same switcher and it has to work for them.
            SetCookie(http, chosen);

            // A signed-in member also gets it saved against the account, so the choice is waiting for
            // them on another device. Best effort: failing to remember a preference must not stop the
            // page from loading in the language they just asked for.
            if (http.User.Identity?.IsAuthenticated == true)
            {
                try
                {
                    await api.SetMyLanguageAsync(chosen, http.RequestAborted);
                }
                catch (Exception)
                {
                    // The cookie already carries the choice for this browser.
                }
            }

            // Only ever back into this site — an open redirect here would be a phishing hop.
            var destination = !string.IsNullOrWhiteSpace(returnUrl) && Uri.IsWellFormedUriString(returnUrl, UriKind.Relative)
                ? returnUrl
                : "/";

            return Results.LocalRedirect(destination);
        })
        .AllowAnonymous()
        .DisableAntiforgery();   // choosing a language changes nothing but this cookie

        return endpoints;
    }

    /// <summary>
    /// Puts a member's saved language back into the cookie when they sign in on a browser that has no
    /// choice of its own. An existing cookie wins: it is what they picked here, just now, and having
    /// signed in should not silently switch the page out from under them.
    /// </summary>
    public static void RestoreLanguageOnSignIn(HttpContext http, string? savedLanguage)
    {
        if (string.IsNullOrWhiteSpace(savedLanguage) || http.Request.Cookies.ContainsKey(KocLanguages.CookieName))
        {
            return;
        }

        SetCookie(http, KocLanguages.Normalize(savedLanguage));
    }

    private static void SetCookie(HttpContext http, string language) =>
        http.Response.Cookies.Append(KocLanguages.CookieName, language, new CookieOptions
        {
            Path = "/",
            Expires = DateTimeOffset.UtcNow.AddYears(1),
            IsEssential = true,          // a language choice is not tracking; it must survive consent settings
            HttpOnly = false,            // the desktop shell and any script may read it
            SameSite = SameSiteMode.Lax,
        });

    /// <summary>The language in force for this request, for the page shell and anything rendering direction.</summary>
    public static string CurrentLanguage(this HttpContext? http) =>
        KocLanguages.Normalize(
            http?.Features.Get<IRequestCultureFeature>()?.RequestCulture.UICulture.TwoLetterISOLanguageName
            ?? CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
}
