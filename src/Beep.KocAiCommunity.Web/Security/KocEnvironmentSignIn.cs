using System.Net.Http.Json;
using System.Security.Claims;
using Beep.KocAiCommunity.Contracts.Identity;
using Beep.KocAiCommunity.ServiceDefaults;
using Beep.KocAiCommunity.ServiceDefaults.Security;
using Microsoft.AspNetCore.Authentication;

namespace Beep.KocAiCommunity.Web.Security;

/// <summary>
/// Signs people in inside KOC, where there is nothing to sign in <em>to</em>: IIS has already verified
/// the Windows account before the request reaches this app. This turns that verified account into the
/// same session a password sign-in would produce — one cookie carrying a platform access token — so
/// everything downstream is identical in both deployments.
/// <para>
/// It also answers the first-run question by observation: a request that arrives already authenticated
/// can only be the corporate deployment, so the app records that and never shows the wizard there.
/// </para>
/// </summary>
public static class KocExchangeClient
{
    /// <summary>
    /// A bare client for the exchange call. Deliberately not the typed API client: that one attaches the
    /// signed-in user's token, and here there is no signed-in user yet — that is the point of the call.
    /// </summary>
    public const string Name = "koc-identity-exchange";
}

public static class KocEnvironmentSignIn
{
    /// <summary>Paths that must not trigger a sign-in attempt (static assets, health, the wizard itself).</summary>
    // "/api" and "/hubs" carry their own bearer token and must not be handed a corporate cookie
    // instead: the platform surface shares this host now, and a desktop client is not a browser.
    private static readonly string[] Skip = ["/health", "/alive", "/api", "/hubs", "/_framework", "/_blazor", "/css", "/js", "/lib", "/brand", "/icons", "/favicon", "/account", "/setup"];

    public static IApplicationBuilder UseKocEnvironmentSignIn(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            var setup = context.RequestServices.GetRequiredService<KocSetupStore>();

            // Already carrying our cookie, or on a path that shouldn't provoke a challenge.
            if (context.User.Identity?.IsAuthenticated == true
                || Skip.Any(s => context.Request.Path.StartsWithSegments(s)))
            {
                await next();
                return;
            }

            // Whatever IIS (or Negotiate) established for this request, if anything.
            var corporate = await CorporateIdentityAsync(context);
            if (corporate is null)
            {
                await next();
                return;
            }

            // A request that arrives already authenticated settles the first-run question on its own.
            if (!setup.IsConfigured)
            {
                setup.Save(new KocSetupState { SignInWith = KocSignInSource.KocEnvironment });
            }

            if (setup.SignInWith != KocSignInSource.KocEnvironment)
            {
                await next();
                return;
            }

            await SignInAsync(context, setup, corporate);
            await next();
        });

    /// <summary>The Windows account IIS put on the request, if any.</summary>
    private static async Task<string?> CorporateIdentityAsync(HttpContext context)
    {
        // IIS in-process publishes the Windows account through its own scheme rather than as the default
        // principal, so ask for it explicitly; Negotiate covers Kestrel and out-of-process hosting.
        foreach (var scheme in (string[])["Windows", "Negotiate"])
        {
            try
            {
                var result = await context.AuthenticateAsync(scheme);
                if (result.Succeeded && result.Principal?.Identity is { IsAuthenticated: true, Name.Length: > 0 } id)
                {
                    return id.Name;
                }
            }
            catch (InvalidOperationException)
            {
                // That scheme isn't registered in this deployment — try the next.
            }
        }

        return null;
    }

    /// <summary>Exchanges the verified account for a platform token and issues our own session cookie.</summary>
    private static async Task SignInAsync(HttpContext context, KocSetupStore setup, string userId)
    {
        var factory = context.RequestServices.GetRequiredService<IHttpClientFactory>();
        var client = factory.CreateClient(KocExchangeClient.Name);

        var at = DateTimeOffset.UtcNow;
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/exchange")
        {
            Content = JsonContent.Create(new ExchangeIdentityRequest(userId, DisplayNameFrom(userId))),
        };
        request.Headers.Add(IdentityExchange.TimestampHeader, at.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture));
        request.Headers.Add(IdentityExchange.SignatureHeader, IdentityExchange.Sign(userId, at, setup.Current.TokenSigningKey));

        var response = await client.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            return; // the request continues unauthenticated; the API will refuse it and the user sees why
        }

        if (await response.Content.ReadFromJsonAsync<AuthTokenResponse>() is not { } auth)
        {
            return;
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, auth.UserId),
            new("oid", auth.UserId),
            new("name", auth.DisplayName),
            new(KocClaims.ApiAccessToken, auth.AccessToken),
        };
        claims.AddRange(auth.Roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var identity = new ClaimsIdentity(claims, SecurityExtensions.WebCookieScheme, "name", ClaimTypes.Role);
        var principal = new ClaimsPrincipal(identity);
        await context.SignInAsync(SecurityExtensions.WebCookieScheme, principal,
            new AuthenticationProperties { IsPersistent = true, ExpiresUtc = auth.ExpiresUtc });

        // Make it effective for *this* request too, not just the next one.
        context.User = principal;
    }

    /// <summary>"KOC\aldhubaib" → "Aldhubaib" until the profile carries a better name.</summary>
    private static string DisplayNameFrom(string userId)
    {
        var local = userId[(userId.LastIndexOf('\\') + 1)..];
        return local.Length == 0 ? userId : char.ToUpperInvariant(local[0]) + local[1..];
    }
}
