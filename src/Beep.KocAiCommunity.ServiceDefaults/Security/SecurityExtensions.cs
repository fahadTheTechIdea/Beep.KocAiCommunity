using System.Text;
using Beep.KocAiCommunity.Application.Security;
using Beep.KocAiCommunity.ServiceDefaults.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Identity.Web;

namespace Beep.KocAiCommunity.ServiceDefaults;

/// <summary>
/// Shared KOC security wiring: the current-user accessor, authorization policies, and authentication
/// chosen by the first-run setup mode (<see cref="KocAuthMode"/>). The Web and the API register
/// different schemes for the same mode — a browser signs in, an API validates a token.
/// </summary>
public static class SecurityExtensions
{
    private const string AzureAdSection = "AzureAd";

    /// <summary>The cookie the Web issues after a local-account sign-in.</summary>
    public const string WebCookieScheme = "KocCookie";

    public static IServiceCollection AddKocCurrentUser(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<IKocCurrentUser, ClaimsKocCurrentUser>();
        return services;
    }

    /// <summary>Registers the setup store both hosts read their authentication mode from.</summary>
    public static IServiceCollection AddKocSetup(this IServiceCollection services)
    {
        services.AddSingleton<KocSetupStore>();
        return services;
    }

    /// <summary>Registers the KOC authorization policies (position and function roles).</summary>
    public static IServiceCollection AddKocAuthorization(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            .AddPolicy(KocPolicies.RequireEmployee, p => p.RequireRole(KocRoles.AllPositions))
            .AddPolicy(KocPolicies.RequirePlatformAdmin, p => p.RequireRole(KocRoles.PlatformAdmin))
            .AddPolicy(KocPolicies.RequireCompetitionAdmin, p => p.RequireRole(KocRoles.CompetitionAdmin, KocRoles.PlatformAdmin))
            .AddPolicy(KocPolicies.RequireLearningAdmin, p => p.RequireRole(KocRoles.LearningAdmin, KocRoles.PlatformAdmin))
            .AddPolicy(KocPolicies.RequireAuditor, p => p.RequireRole(KocRoles.Auditor, KocRoles.PlatformAdmin))
            .AddPolicy(KocPolicies.RequireSupervisor, p => p.RequireRole([.. KocRoles.SupervisorPositions, KocRoles.PlatformAdmin]));

        return services;
    }

    /// <summary>
    /// How a browser signs in to the Web, per the configured mode: a cookie for local accounts, intranet
    /// Negotiate SSO, Entra OIDC, or nothing at all in demo/unconfigured mode (where the persona switcher
    /// stands in for a real identity and the setup wizard must stay reachable).
    /// </summary>
    public static IServiceCollection AddKocWebAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var setup = new KocSetupStore(configuration);
        switch (setup.Mode)
        {
            case KocAuthMode.LocalAccounts:
                services.AddAuthentication(WebCookieScheme)
                    .AddCookie(WebCookieScheme, options =>
                    {
                        options.LoginPath = "/account/login";
                        options.LogoutPath = "/account/logout";
                        options.AccessDeniedPath = "/account/login";
                        options.ExpireTimeSpan = TimeSpan.FromHours(8);
                        options.SlidingExpiration = true;
                        options.Cookie.Name = "koc.auth";
                        options.Cookie.HttpOnly = true;
                        options.Cookie.SameSite = SameSiteMode.Lax;
                        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                    });
                break;

            case KocAuthMode.EntraId:
                services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
                    .AddMicrosoftIdentityWebApp(configuration.GetSection(AzureAdSection));
                break;

            case KocAuthMode.WindowsIntranet:
                // Intranet SSO — the browser hands the site the signed-in Windows/Entra account with no
                // login page. Enable "Windows Authentication" (and disable Anonymous) on the IIS site;
                // Negotiate also covers Kestrel/HTTP.sys and IIS out-of-process hosting.
                services.AddAuthentication(NegotiateDefaults.AuthenticationScheme).AddNegotiate();
                break;

            default:
                AddDevFallback(services);
                break;
        }

        return services;
    }

    /// <summary>
    /// How the API decides who a caller is: a signed access token for local accounts, Entra JWTs for the
    /// corporate tenant, and the header-driven dev identity only in demo mode — never otherwise, so a
    /// reachable API can't be impersonated with a plain <c>X-Dev-User</c> header.
    /// </summary>
    public static IServiceCollection AddKocApiAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var setup = new KocSetupStore(configuration);
        switch (setup.Mode)
        {
            case KocAuthMode.LocalAccounts:
                services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                    .AddJwtBearer(options =>
                    {
                        options.TokenValidationParameters = TokenValidation(setup.Current.TokenSigningKey);
                        options.MapInboundClaims = false;
                    });
                break;

            case KocAuthMode.EntraId:
                services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                    .AddMicrosoftIdentityWebApi(configuration.GetSection(AzureAdSection));
                break;

            case KocAuthMode.WindowsIntranet:
                // The Web is the only caller and runs on the same trusted network; it forwards the
                // authenticated Windows account. Negotiate also lets tools call the API directly.
                services.AddAuthentication(NegotiateDefaults.AuthenticationScheme).AddNegotiate();
                break;

            case KocAuthMode.DemoPersonas:
                // Demo only: authenticate every request as the persona the Web forwards in headers.
                services.AddAuthentication(DevAutoAuthHandler.SchemeName)
                    .AddScheme<AuthenticationSchemeOptions, DevAutoAuthHandler>(DevAutoAuthHandler.SchemeName, _ => { });
                break;

            default:
                AddDevFallback(services);
                break;
        }

        return services;
    }

    /// <summary>
    /// Validation parameters for the API's own access tokens — shared by the issuer and the validator so
    /// they cannot drift. Returns parameters that reject everything when no key is configured yet.
    /// </summary>
    public static TokenValidationParameters TokenValidation(string? signingKeyBase64) => new()
    {
        ValidateIssuer = true,
        ValidIssuer = TokenIssuer,
        ValidateAudience = true,
        ValidAudience = TokenAudience,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = SigningKey(signingKeyBase64),
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromMinutes(1),
        RoleClaimType = System.Security.Claims.ClaimTypes.Role,
        NameClaimType = "name",
    };

    /// <summary>The issuer/audience stamped on the API's access tokens.</summary>
    public const string TokenIssuer = "koc-ai-community";

    /// <summary>The audience stamped on the API's access tokens.</summary>
    public const string TokenAudience = "koc-ai-community-api";

    /// <summary>The symmetric signing key, or a random unusable one when setup hasn't produced one yet.</summary>
    public static SymmetricSecurityKey SigningKey(string? signingKeyBase64)
    {
        if (!string.IsNullOrWhiteSpace(signingKeyBase64))
        {
            try
            {
                return new SymmetricSecurityKey(Convert.FromBase64String(signingKeyBase64));
            }
            catch (FormatException)
            {
                // Not base64 — treat it as a passphrase rather than failing to boot.
                return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKeyBase64.PadRight(32, '.')));
            }
        }

        // No key yet: sign/validate against ephemeral bytes so any token presented is rejected.
        return new SymmetricSecurityKey(Convert.FromBase64String(KocSetupStore.NewSigningKey()));
    }

    /// <summary>True when intranet Windows (Negotiate) authentication is opted in via <c>WindowsAuth:Enabled</c>.</summary>
    public static bool IsWindowsAuthEnabled(IConfiguration configuration) =>
        configuration.GetValue("WindowsAuth:Enabled", false);

    private static void AddDevFallback(IServiceCollection services) =>
        services.AddAuthentication(DevFallbackAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, DevFallbackAuthHandler>(DevFallbackAuthHandler.SchemeName, _ => { });

    /// <summary>True when a KOC Entra tenant + client id are present in configuration.</summary>
    public static bool IsEntraConfigured(IConfiguration configuration)
    {
        var section = configuration.GetSection(AzureAdSection);
        return section.Exists()
            && !string.IsNullOrWhiteSpace(section["TenantId"])
            && !string.IsNullOrWhiteSpace(section["ClientId"]);
    }
}
