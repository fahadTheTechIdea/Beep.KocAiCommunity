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
    // "/api" and "/hubs" are here because the platform surface shares this host now: a desktop client
    // asking for competitions must get JSON or a 401, never an HTML redirect to a wizard it cannot show.
    private static readonly string[] AlwaysAllowed = ["/setup", "/health", "/alive", "/api", "/hubs", "/_framework", "/_blazor", "/css", "/js", "/lib", "/brand", "/icons", "/favicon"];

    public static IEndpointRouteBuilder MapKocSetupEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/setup", (HttpContext context, [FromForm] string signInWith, KocSetupStore setup) =>
        {
            // Once settled, setup is closed. Re-running it would let anyone who can reach the site point
            // authentication somewhere they control and walk in.
            if (setup.IsConfigured)
            {
                return Results.Redirect("/");
            }

            if (!Enum.TryParse<KocSignInSource>(signInWith, ignoreCase: true, out var chosen)
                || chosen == KocSignInSource.Unconfigured)
            {
                return Results.Redirect("/setup?error=Choose+where+people+will+sign+in.");
            }

            setup.Save(new KocSetupState { SignInWith = chosen });
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
