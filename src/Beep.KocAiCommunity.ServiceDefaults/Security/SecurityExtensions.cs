using Beep.KocAiCommunity.Application.Security;
using Beep.KocAiCommunity.ServiceDefaults.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Identity.Web;

namespace Beep.KocAiCommunity.ServiceDefaults;

/// <summary>
/// Shared KOC security wiring: the current-user accessor, authorization policies, and
/// config-driven Entra authentication. When the <c>AzureAd</c> section is absent (local dev
/// without a tenant) authentication is skipped so the app still runs.
/// </summary>
public static class SecurityExtensions
{
    private const string AzureAdSection = "AzureAd";

    public static IServiceCollection AddKocCurrentUser(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<IKocCurrentUser, ClaimsKocCurrentUser>();
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
    /// Web OIDC sign-in against the KOC Entra tenant when configured; otherwise registers the base
    /// authentication services so the middleware runs and tests can add their own scheme.
    /// </summary>
    public static IServiceCollection AddKocWebAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        if (IsEntraConfigured(configuration))
        {
            services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
                .AddMicrosoftIdentityWebApp(configuration.GetSection(AzureAdSection));
        }
        else if (IsWindowsAuthEnabled(configuration))
        {
            // Intranet SSO — the browser hands the site the signed-in Windows/Entra account with no
            // login page. Enable "Windows Authentication" (and disable Anonymous) on the IIS site;
            // Negotiate also covers Kestrel/HTTP.sys and IIS out-of-process hosting.
            services.AddAuthentication(NegotiateDefaults.AuthenticationScheme).AddNegotiate();
        }
        else
        {
            AddDevFallback(services);
        }

        return services;
    }

    /// <summary>True when intranet Windows (Negotiate) authentication is opted in via <c>WindowsAuth:Enabled</c>.</summary>
    public static bool IsWindowsAuthEnabled(IConfiguration configuration) =>
        configuration.GetValue("WindowsAuth:Enabled", false);

    /// <summary>
    /// API JWT bearer validation against the KOC Entra tenant when configured; otherwise registers
    /// the base authentication services so the middleware runs and tests can add their own scheme.
    /// </summary>
    public static IServiceCollection AddKocApiAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        if (IsEntraConfigured(configuration))
        {
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddMicrosoftIdentityWebApi(configuration.GetSection(AzureAdSection));
        }
        else if (configuration.GetValue("DevAuth:Enabled", false))
        {
            // Development convenience: authenticate every request as a dev user so the API is
            // usable without a real tenant. Never enabled in production (guard by config).
            services.AddAuthentication(DevAutoAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, DevAutoAuthHandler>(DevAutoAuthHandler.SchemeName, _ => { });
        }
        else
        {
            AddDevFallback(services);
        }

        return services;
    }

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
