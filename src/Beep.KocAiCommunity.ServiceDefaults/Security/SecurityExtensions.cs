using System.Text;
using Beep.KocAiCommunity.Application.Security;
using Beep.KocAiCommunity.ServiceDefaults.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Beep.KocAiCommunity.ServiceDefaults;

/// <summary>
/// Shared KOC security wiring: the current-user accessor, the authorization policies, and the sign-in
/// schemes.
/// <para>
/// The API is deliberately uniform — it validates <em>this platform's</em> access token and nothing else,
/// in every deployment. Where a person originally proved themselves (a password here, the corporate
/// intranet, Entra) is the Web's concern: it verifies them and exchanges that for a site token. So the
/// API has one scheme instead of a branch per deployment, and authorization behaves identically
/// everywhere.
/// </para>
/// </summary>
public static class SecurityExtensions
{
    private const string AzureAdSection = "AzureAd";

    /// <summary>The cookie the Web issues once someone has signed in, however they did it.</summary>
    public const string WebCookieScheme = "KocCookie";

    public static IServiceCollection AddKocCurrentUser(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<IKocCurrentUser, ClaimsKocCurrentUser>();
        return services;
    }

    /// <summary>Registers the setup store both hosts read the first-run answer from.</summary>
    public static IServiceCollection AddKocSetup(this IServiceCollection services)
    {
        services.AddSingleton<KocSetupStore>();
        return services;
    }

    /// <summary>Registers the KOC authorization policies (position and function roles).</summary>
    public static IServiceCollection AddKocAuthorization(this IServiceCollection services)
    {
        // Policies deliberately name no scheme: they follow whatever the host made the default. That is
        // what lets a test host swap the whole authentication stack for a stub and still exercise these
        // rules. Where one host serves both browsers and API callers, the default is a routing scheme
        // that picks by credential — see AddKocDualAuthentication.
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
    /// The API's only scheme: this platform's access token. It does not care how the holder originally
    /// signed in, so nothing about the API changes between a KOC deployment and a public one.
    /// </summary>
    public static IServiceCollection AddKocApiAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var setup = new KocSetupStore(configuration);
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = TokenValidation(setup.Current.TokenSigningKey);
                options.MapInboundClaims = false;
            });

        return services;
    }

    /// <summary>
    /// Adds the platform's access-token scheme <em>beside</em> whatever the host already uses.
    /// <para>
    /// <see cref="AddKocApiAuthentication"/> makes the token the default scheme, which is right for a
    /// host that serves nothing else. The website is not that host: it signs people in with a cookie
    /// and must keep the cookie as its default, while still accepting a bearer token from KOC Studio.
    /// </para>
    /// </summary>
    public static IServiceCollection AddKocPlatformBearer(this IServiceCollection services, IConfiguration configuration)
    {
        var setup = new KocSetupStore(configuration);

        // The default is a routing scheme, not either real one. A request carrying "Authorization:
        // Bearer" is an API caller and is validated as a token; anything else is a browser and is
        // validated as our sign-in cookie. One default keeps every policy — and any host that replaces
        // the default wholesale, as the tests do — working unchanged.
        services.AddAuthentication(DualScheme)
            .AddPolicyScheme(DualScheme, DualScheme, options =>
            {
                options.ForwardDefaultSelector = context =>
                    // The API surface is always token-authenticated, whether or not this particular
                    // request carried one. Falling back to the cookie for an unauthenticated /api call
                    // answered it with a 302 to the sign-in page — a redirect is not something an API
                    // client can act on, and the contract there is 401.
                    context.Request.Path.StartsWithSegments("/api")
                    || context.Request.Path.StartsWithSegments("/hubs")
                    || context.Request.Headers.Authorization.ToString()
                        .StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                        ? JwtBearerDefaults.AuthenticationScheme
                        : WebCookieScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = TokenValidation(setup.Current.TokenSigningKey);
                options.MapInboundClaims = false;
            });

        return services;
    }

    /// <summary>The routing scheme a host uses when it serves both browsers and API callers.</summary>
    public const string DualScheme = "KocCookieOrToken";

    /// <summary>
    /// The browser-facing cookie the Web signs people into. Always registered: whether the credentials
    /// were checked here or by the corporate environment, the resulting session is the same cookie.
    /// The corporate challenge handlers are added alongside it by the Web when that is where people
    /// sign in.
    /// </summary>
    public static IServiceCollection AddKocWebAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
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

        return services;
    }

    /// <summary>
    /// Validation parameters for the platform's access tokens — shared by the issuer and the validator so
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

    /// <summary>The issuer stamped on the platform's access tokens.</summary>
    public const string TokenIssuer = "koc-ai-community";

    /// <summary>The audience stamped on the platform's access tokens.</summary>
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

    /// <summary>True when a KOC Entra tenant + client id are present in configuration.</summary>
    public static bool IsEntraConfigured(IConfiguration configuration)
    {
        var section = configuration.GetSection(AzureAdSection);
        return section.Exists()
            && !string.IsNullOrWhiteSpace(section["TenantId"])
            && !string.IsNullOrWhiteSpace(section["ClientId"]);
    }
}
