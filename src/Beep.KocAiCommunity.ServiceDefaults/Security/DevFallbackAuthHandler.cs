using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Beep.KocAiCommunity.ServiceDefaults.Security;

/// <summary>
/// Fallback scheme used only when Entra is not configured (local dev without a tenant). Every
/// request is anonymous, and protected endpoints challenge with a clean 401 instead of throwing
/// "no default challenge scheme". Real sign-in requires the Entra configuration.
/// </summary>
public sealed class DevFallbackAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "DevFallback";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync() =>
        Task.FromResult(AuthenticateResult.NoResult());
}
