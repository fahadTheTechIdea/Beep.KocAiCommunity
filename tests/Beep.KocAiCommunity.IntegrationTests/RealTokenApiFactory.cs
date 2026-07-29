using Microsoft.Extensions.DependencyInjection;

namespace Beep.KocAiCommunity.IntegrationTests;

/// <summary>
/// The API with its <b>real</b> token validation left in place, rather than the header-driven test
/// scheme. Use this wherever the point is that a token issued by the platform is accepted — password
/// sign-in, and the exchange that turns a corporate account into one. The sign-in source it boots with
/// is incidental: the API validates the same token however the holder originally proved themselves.
/// </summary>
public sealed class RealTokenApiFactory : KocApiFactory
{
    protected override string AuthMode => "SiteAccounts";

    /// <summary>Keep the host's own JWT bearer validation — that is the thing under test.</summary>
    protected override void ConfigureAuthentication(IServiceCollection services)
    {
    }
}
