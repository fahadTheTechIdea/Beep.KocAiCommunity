using Beep.KocAiCommunity.ServiceDefaults.Security;
using Microsoft.AspNetCore.Mvc;

namespace Beep.KocAiCommunity.Web.Security;

/// <summary>
/// The first-run wizard's save, plus the guard that makes it unavoidable while the app is unconfigured
/// and unreachable once it isn't.
/// </summary>
public static class SetupEndpoints
{
    /// <summary>Paths that must stay reachable while the app is still unconfigured.</summary>
    private static readonly string[] AlwaysAllowed = ["/setup", "/health", "/alive", "/_framework", "/_blazor", "/css", "/js", "/lib", "/brand", "/icons", "/favicon"];

    public static IEndpointRouteBuilder MapKocSetupEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/setup", (
            HttpContext context,
            [FromForm] string mode,
            [FromForm] string? entraTenantId,
            [FromForm] string? entraClientId,
            [FromForm] string? entraClientSecret,
            KocSetupStore setup) =>
        {
            // Once configured, setup is closed. Re-running it would let anyone who can reach the site
            // swap the app to demo mode and walk in as an administrator.
            if (setup.IsConfigured)
            {
                return Results.Redirect("/");
            }

            if (!Enum.TryParse<KocAuthMode>(mode, ignoreCase: true, out var chosen) || chosen == KocAuthMode.Unconfigured)
            {
                return Results.Redirect("/setup?error=Choose+how+people+will+sign+in.");
            }

            if (chosen == KocAuthMode.EntraId && (string.IsNullOrWhiteSpace(entraTenantId) || string.IsNullOrWhiteSpace(entraClientId)))
            {
                return Results.Redirect("/setup?error=Entra+sign-in+needs+both+a+tenant+id+and+a+client+id.&mode=EntraId");
            }

            setup.Save(new KocSetupState
            {
                Mode = chosen,
                EntraTenantId = entraTenantId?.Trim(),
                EntraClientId = entraClientId?.Trim(),
                EntraClientSecret = entraClientSecret?.Trim(),
            });

            return Results.Redirect("/setup?saved=1");
        })
        .AllowAnonymous()
        .DisableAntiforgery();  // served by static SSR before any circuit exists

        return app;
    }

    /// <summary>
    /// While no sign-in mode has been chosen, every request lands on the wizard. Without this the app
    /// would render its shell against an API that rejects it, which reads as broken rather than unfinished.
    /// </summary>
    public static IApplicationBuilder UseKocSetupGuard(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            var setup = context.RequestServices.GetRequiredService<KocSetupStore>();
            var path = context.Request.Path;

            if (setup.IsConfigured)
            {
                // Configured: the wizard is done and stays closed (a GET would otherwise offer to redo it).
                if (path.StartsWithSegments("/setup"))
                {
                    context.Response.Redirect("/");
                    return;
                }
            }
            else if (!AlwaysAllowed.Any(allowed => path.StartsWithSegments(allowed)))
            {
                context.Response.Redirect("/setup");
                return;
            }

            await next();
        });
}
