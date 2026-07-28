namespace Beep.KocAiCommunity.Web.Services;

/// <summary>
/// When intranet Windows auth is on, forwards the <b>real signed-in user</b> to the API — overriding
/// the dev-persona headers set upstream. Fail-closed: if no authenticated user is present it strips the
/// identity headers entirely (the API then sees an unauthenticated call → 401) rather than leaking the
/// default persona.
/// <para>
/// Identity only — no roles. What the corporate account may do is decided by the platform's own
/// database (see <c>AppDatabaseRoleClaims</c>), so that an administrator changing someone's roles in the
/// RBAC console takes effect regardless of how that person signs in.
/// </para>
///
/// Note: reads the principal via <see cref="IHttpContextAccessor"/>. Under IIS Windows Authentication
/// (in-process) this carries the signed-in user; validate interactive Blazor Server calls in the KOC
/// environment — the fully robust variant reads <c>AuthenticationStateProvider</c> per circuit.
/// </summary>
public sealed class WindowsIdentityForwardingHandler(IHttpContextAccessor accessor) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Always drop any persona headers a prior handler set — the real user is authoritative here.
        request.Headers.Remove("X-Dev-User");
        request.Headers.Remove("X-Dev-Roles");

        if (accessor.HttpContext?.User.Identity is { IsAuthenticated: true, Name.Length: > 0 } id)
        {
            request.Headers.Add("X-Dev-User", id.Name!);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
