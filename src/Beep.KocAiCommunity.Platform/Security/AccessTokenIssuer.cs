using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Beep.KocAiCommunity.ServiceDefaults;
using Beep.KocAiCommunity.ServiceDefaults.Security;
using Microsoft.IdentityModel.Tokens;

namespace Beep.KocAiCommunity.Platform.Security;

/// <summary>
/// Mints the access tokens the Web sends back on every API call after a local-account sign-in. Signed
/// with the key the first-run setup generated and shared with the Web, and carrying exactly the claims
/// <c>ClaimsKocCurrentUser</c> reads: the user id, a display name, and the KOC roles.
/// </summary>
public sealed class AccessTokenIssuer(KocSetupStore setup)
{
    /// <summary>How long an access token stays valid. The Web's cookie expires on the same clock.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(8);

    public (string Token, DateTime ExpiresUtc) Issue(string userId, string displayName, IReadOnlyList<string> roles)
    {
        var expires = DateTime.UtcNow.Add(Lifetime);
        var claims = new List<Claim>
        {
            // "oid" first, then NameIdentifier — the same order the claims reader prefers, so a local
            // account and an Entra account resolve to a user id identically.
            new("oid", userId),
            new(ClaimTypes.NameIdentifier, userId),
            new("name", displayName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var token = new JwtSecurityToken(
            issuer: SecurityExtensions.TokenIssuer,
            audience: SecurityExtensions.TokenAudience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expires,
            signingCredentials: new SigningCredentials(
                SecurityExtensions.SigningKey(setup.Current.TokenSigningKey),
                SecurityAlgorithms.HmacSha256));

        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}
