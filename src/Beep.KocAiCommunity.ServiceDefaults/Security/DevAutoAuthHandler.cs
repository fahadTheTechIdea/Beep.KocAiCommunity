using System.Security.Claims;
using System.Text.Encodings.Web;
using Beep.KocAiCommunity.Application.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Beep.KocAiCommunity.ServiceDefaults.Security;

/// <summary>
/// Development-only authentication so the API is callable without a real Entra tenant. Every
/// request is authenticated as a dev user — taken from the <c>X-Dev-User</c>/<c>X-Dev-Roles</c>
/// headers when present (so the Web can forward its selected identity), otherwise the configured
/// <c>DevAuth:UserId</c>/<c>DevAuth:Roles</c> defaults. Never registered when Entra is configured.
/// </summary>
public sealed class DevAutoAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IConfiguration configuration) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "DevAuto";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var userId = Request.Headers.TryGetValue("X-Dev-User", out var headerUser) && !string.IsNullOrWhiteSpace(headerUser)
            ? headerUser.ToString()
            : configuration["DevAuth:UserId"] ?? "dev-user";

        var roles = Request.Headers.TryGetValue("X-Dev-Roles", out var headerRoles) && !string.IsNullOrWhiteSpace(headerRoles)
            ? headerRoles.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : (configuration["DevAuth:Roles"] ?? KocRoles.Employee).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var claims = new List<Claim>
        {
            new("oid", userId),
            new(ClaimTypes.NameIdentifier, userId),
            new("name", userId),
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var identity = new ClaimsIdentity(claims, SchemeName);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
