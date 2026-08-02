using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Beep.KocAiCommunity.Platform;

/// <summary>
/// Keeps <c>/api/v1</c> to this machine.
/// <para>
/// When the API was its own website it had to be reachable — the website and KOC Studio both called it
/// across the network. Neither does any more: the website carries the surface in its own process and
/// calls it over loopback, and the desktop reads the database directly. That leaves a public API with
/// no caller, which is attack surface being maintained for nobody.
/// </para>
/// <para>
/// The leaderboard hub is deliberately <b>not</b> covered. KOC Studio still connects to it from a
/// workstation for live standings, so it is the one part of the surface with a genuine remote caller.
/// </para>
/// </summary>
public static class InternalApiGuard
{
    /// <summary>Set false to expose the API again — for a deployment that reintroduces a remote client.</summary>
    public const string ConfigurationKey = "Platform:InternalApiOnly";

    public static IApplicationBuilder UseKocInternalApiOnly(this IApplicationBuilder app, bool internalOnly = true)
    {
        if (!internalOnly)
        {
            return app;
        }

        return app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments(PlatformApi.RoutePrefix) && !IsLocal(context))
            {
                // 404, not 403. A refusal tells someone probing that there is something here to find;
                // "no such path" is the truthful answer from outside this machine anyway.
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            await next();
        });
    }

    /// <summary>
    /// Whether the call came from this machine.
    /// <para>
    /// A null remote address means the request never crossed a socket — an in-process host, which is
    /// how the test server and any future in-process caller arrive. That is as local as it gets.
    /// </para>
    /// </summary>
    private static bool IsLocal(HttpContext context)
    {
        var remote = context.Connection.RemoteIpAddress;
        if (remote is null)
        {
            return true;
        }

        var local = context.Connection.LocalIpAddress;
        return System.Net.IPAddress.IsLoopback(remote) || (local is not null && remote.Equals(local));
    }
}
