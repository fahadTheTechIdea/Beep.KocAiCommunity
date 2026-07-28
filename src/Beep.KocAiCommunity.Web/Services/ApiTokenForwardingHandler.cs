using System.Net.Http.Headers;
using System.Security.Claims;
using Beep.KocAiCommunity.Web.Security;
using Microsoft.AspNetCore.Components.Authorization;

namespace Beep.KocAiCommunity.Web.Services;

/// <summary>
/// Attaches the signed-in user's API access token to every outgoing API call, so the API authenticates
/// the real person instead of trusting a header. The token is carried as a claim inside the (encrypted)
/// auth cookie.
/// <para>
/// The user has to be found two different ways, because API calls originate from two different worlds.
/// A plain HTTP request — a static-SSR page render, or the sign-in form posts — has an
/// <c>HttpContext</c>. An interactive Blazor component runs over a SignalR circuit long after that
/// request ended, and there the circuit's <see cref="AuthenticationStateProvider"/> is the only source;
/// asking it outside a component scope throws, which is why it is the fallback rather than the default.
/// </para>
/// <para>Fail-closed: no token found means no Authorization header, and the API answers 401.</para>
/// </summary>
public sealed class ApiTokenForwardingHandler(
    IHttpContextAccessor httpContext,
    IServiceProvider services) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Never let a stale persona header ride along — the token is the only identity that counts here.
        request.Headers.Remove("X-Dev-User");
        request.Headers.Remove("X-Dev-Roles");

        if (await TokenAsync() is { Length: > 0 } token)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await base.SendAsync(request, cancellationToken);
    }

    private async Task<string?> TokenAsync()
    {
        if (httpContext.HttpContext?.User is { Identity.IsAuthenticated: true } user)
        {
            return Token(user);
        }

        // No live request: we're inside a circuit, so ask it who is connected.
        var provider = services.GetService<AuthenticationStateProvider>();
        if (provider is null)
        {
            return null;
        }

        try
        {
            return Token((await provider.GetAuthenticationStateAsync()).User);
        }
        catch (InvalidOperationException)
        {
            // Neither a request nor a component scope — an anonymous call (sign-in itself, say).
            return null;
        }
    }

    private static string? Token(ClaimsPrincipal user) => user.FindFirst(KocClaims.ApiAccessToken)?.Value;
}
