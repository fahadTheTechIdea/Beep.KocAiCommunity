using System.Security.Claims;
using Beep.KocAiCommunity.Application.Security;
using Microsoft.AspNetCore.Authentication;

namespace Beep.KocAiCommunity.Platform.Security;

/// <summary>
/// Makes the platform's own database the authority on what a caller may do, whatever proved who they are.
/// <para>
/// Sign-in differs per deployment — a password on this site, the corporate intranet account, or Entra —
/// but the roles are ours. After authentication this replaces whatever role claims arrived (Entra app
/// roles, or none at all from Negotiate) with the roles recorded here, and records the person on their
/// first visit so an intranet user exists in the RBAC console without an administrator pre-creating them.
/// </para>
/// <para>
/// Not registered in demo mode: there the switchable persona <em>is</em> the point, so the roles it
/// asserts stand.
/// </para>
/// </summary>
public sealed class AppDatabaseRoleClaims(IKocUserDirectory directory) : IClaimsTransformation
{
    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity is not { IsAuthenticated: true } identity)
        {
            return principal;
        }

        var userId = principal.FindFirstValue("oid")
            ?? principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? identity.Name;
        if (string.IsNullOrWhiteSpace(userId))
        {
            return principal;
        }

        // First sight of this person records them (and, on an empty install, makes them the admin).
        // Afterwards it is a straight read of the roles an administrator has assigned.
        var roles = await directory.EnsureUserAsync(userId, principal.FindFirstValue("name"), principal.FindFirstValue(ClaimTypes.Email));

        // Rebuild rather than mutate: the same principal can be transformed more than once per request,
        // and appending would stack duplicate role claims.
        var kept = principal.Claims.Where(c => c.Type != ClaimTypes.Role && c.Type != "roles");
        var rebuilt = new ClaimsIdentity(
            [.. kept, .. roles.Select(r => new Claim(ClaimTypes.Role, r))],
            identity.AuthenticationType,
            principal.Identities.FirstOrDefault()?.NameClaimType ?? "name",
            ClaimTypes.Role);

        // Carry the id forward under the name the claims reader looks for first, so a Negotiate sign-in
        // (which only supplies a Windows name) resolves to the same user id as everything else.
        if (rebuilt.FindFirst("oid") is null)
        {
            rebuilt.AddClaim(new Claim("oid", userId));
        }

        return new ClaimsPrincipal(rebuilt);
    }
}
