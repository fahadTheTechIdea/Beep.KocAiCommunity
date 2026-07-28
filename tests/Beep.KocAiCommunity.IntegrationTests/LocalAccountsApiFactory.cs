using Microsoft.Extensions.DependencyInjection;

namespace Beep.KocAiCommunity.IntegrationTests;

/// <summary>
/// The API booted the way a normal web deployment runs it: accounts held by this app, and access tokens
/// it issues and validates itself. Unlike <see cref="KocApiFactory"/> this deliberately does <b>not</b>
/// install the header-driven test scheme — otherwise the register/sign-in path would be bypassed and the
/// tests would prove nothing about it.
/// </summary>
public sealed class LocalAccountsApiFactory : KocApiFactory
{
    protected override string AuthMode => "LocalAccounts";

    /// <summary>Keep the host's real JWT bearer validation — that is the thing under test.</summary>
    protected override void ConfigureAuthentication(IServiceCollection services)
    {
    }
}
