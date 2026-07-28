using System.Security.Claims;
using Beep.KocAiCommunity.Client;
using Beep.KocAiCommunity.Contracts.Identity;
using Beep.KocAiCommunity.ServiceDefaults;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace Beep.KocAiCommunity.Web.Security;

/// <summary>
/// The sign-in form posts. These are plain HTTP endpoints rather than Blazor component handlers because
/// writing an authentication cookie needs a live response — an interactive circuit runs over a WebSocket
/// long after the headers are sent, so <c>SignInAsync</c> there would be too late to do anything.
/// <para>
/// Each one asks the API to verify the credentials, then turns the API's access token into a local cookie.
/// The token rides along as a claim so every later API call can present it.
/// </para>
/// </summary>
public static class AccountEndpoints
{
    public static IEndpointRouteBuilder MapKocAccountEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/account/login", async (
            HttpContext context, [FromForm] string email, [FromForm] string password, [FromForm] string? returnUrl, IKocApiClient api) =>
        {
            var (auth, error) = await api.LoginAsync(new LoginRequest(email ?? string.Empty, password ?? string.Empty));
            if (auth is null)
            {
                return Redirect("/account/login", returnUrl, error ?? "Sign-in failed.");
            }

            await SignInAsync(context, auth);
            return Results.Redirect(SafeReturnUrl(returnUrl));
        })
        .AllowAnonymous()
        .DisableAntiforgery();  // the form is served by static SSR before any circuit/token exists

        app.MapPost("/account/register", async (
            HttpContext context, [FromForm] string email, [FromForm] string password, [FromForm] string? displayName,
            [FromForm] string? returnUrl, IKocApiClient api) =>
        {
            var (auth, error) = await api.RegisterAsync(new RegisterRequest(email ?? string.Empty, password ?? string.Empty, displayName));
            if (auth is null)
            {
                return Redirect("/account/register", returnUrl, error ?? "Registration failed.");
            }

            await SignInAsync(context, auth);
            return Results.Redirect(SafeReturnUrl(returnUrl));
        })
        .AllowAnonymous()
        .DisableAntiforgery();

        app.MapPost("/account/logout", async (HttpContext context) =>
        {
            await context.SignOutAsync(SecurityExtensions.WebCookieScheme);
            return Results.Redirect("/");
        });

        return app;
    }

    private static async Task SignInAsync(HttpContext context, AuthTokenResponse auth)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, auth.UserId),
            new("oid", auth.UserId),
            new("name", auth.DisplayName),
            new(KocClaims.ApiAccessToken, auth.AccessToken),
        };
        claims.AddRange(auth.Roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var identity = new ClaimsIdentity(claims, SecurityExtensions.WebCookieScheme, "name", ClaimTypes.Role);
        await context.SignInAsync(
            SecurityExtensions.WebCookieScheme,
            new ClaimsPrincipal(identity),
            // The cookie must not outlive the API token it carries, or the user appears signed in while
            // every API call returns 401.
            new AuthenticationProperties { IsPersistent = true, ExpiresUtc = auth.ExpiresUtc });
    }

    /// <summary>Back to the form with the failure to show, preserving where the user was headed.</summary>
    private static IResult Redirect(string page, string? returnUrl, string error) =>
        Results.Redirect(QueryHelpers.AddQueryString(page, new Dictionary<string, string?>
        {
            ["error"] = error,
            ["returnUrl"] = string.IsNullOrWhiteSpace(returnUrl) ? null : returnUrl,
        }));

    /// <summary>
    /// Only ever redirect within this site. An attacker-supplied absolute URL in <c>returnUrl</c> would
    /// otherwise turn the login page into an open redirect.
    /// </summary>
    private static string SafeReturnUrl(string? returnUrl) =>
        !string.IsNullOrWhiteSpace(returnUrl) && returnUrl.StartsWith('/') && !returnUrl.StartsWith("//")
            ? returnUrl
            : "/";
}
