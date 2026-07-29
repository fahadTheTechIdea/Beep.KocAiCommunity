using Beep.KocAiCommunity.ServiceDefaults.Security;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Beep.KocAiCommunity.UnitTests;

/// <summary>
/// The first run settles one thing: where people sign in. Everything else the platform needs is managed
/// inside the site once it is running, so this file is deliberately small — if it grows, something has
/// been pushed into startup that didn't belong there.
/// </summary>
public class KocSetupStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"koc-setup-{Guid.NewGuid():N}");

    private KocSetupStore Store(params (string Key, string? Value)[] settings)
    {
        var values = settings.ToDictionary(s => s.Key, s => s.Value);
        values["Setup:File"] = Path.Combine(_directory, "setup.json");
        return new KocSetupStore(new ConfigurationBuilder().AddInMemoryCollection(values).Build());
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    [Fact]
    public void A_fresh_install_has_no_answer_yet()
    {
        var store = Store();

        store.IsConfigured.Should().BeFalse();
        store.SignInWith.Should().Be(KocSignInSource.Unconfigured);
    }

    [Fact]
    public void The_answer_persists_for_the_other_process_to_read()
    {
        Store().Save(new KocSetupState { SignInWith = KocSignInSource.SiteAccounts });

        // A second store over the same file is the API reading what the Web wrote.
        var reread = Store();
        reread.SignInWith.Should().Be(KocSignInSource.SiteAccounts);
        reread.IsConfigured.Should().BeTrue();
        reread.Current.CompletedUtc.Should().NotBeNull();
    }

    [Fact]
    public void Both_hosts_get_the_same_signing_key()
    {
        // The API signs access tokens with it and the Web proves itself with it when vouching for a
        // corporate account. Two different keys means every call is rejected.
        var saved = Store().Save(new KocSetupState { SignInWith = KocSignInSource.KocEnvironment });

        saved.TokenSigningKey.Should().NotBeNullOrWhiteSpace();
        Convert.FromBase64String(saved.TokenSigningKey!).Should().HaveCount(32);
        Store().Current.TokenSigningKey.Should().Be(saved.TokenSigningKey);
    }

    [Fact]
    public void An_explicit_answer_in_configuration_wins_over_the_file()
    {
        Store().Save(new KocSetupState { SignInWith = KocSignInSource.KocEnvironment });

        // A deployment that states where people sign in is already set up and never sees the wizard,
        // whatever a stale file on that machine happens to say.
        Store(("Auth:SignInWith", "SiteAccounts")).SignInWith.Should().Be(KocSignInSource.SiteAccounts);
    }

    [Theory]
    [InlineData("WindowsAuth:Enabled", "true")]
    [InlineData("AzureAd:TenantId", "tenant")]
    public void Deployments_configured_before_the_wizard_existed_keep_working(string key, string value)
    {
        // These older keys said "the corporate environment authenticates people" — still true.
        var extra = key == "AzureAd:TenantId" ? ("AzureAd:ClientId", "client") : ("Unused", (string?)null);
        Store((key, value), extra!).SignInWith.Should().Be(KocSignInSource.KocEnvironment);
    }

    [Fact]
    public void Demo_personas_are_a_configuration_flag_not_a_first_run_choice()
    {
        // Picking who to be is a development convenience, never something a deployment is asked at setup.
        Store().DemoPersonasEnabled.Should().BeFalse();
        Store(("Auth:DemoPersonas", "true")).DemoPersonasEnabled.Should().BeTrue();
        Store(("DevAuth:Enabled", "true")).DemoPersonasEnabled.Should().BeTrue();

        // And it says nothing about where real people sign in.
        Store(("Auth:DemoPersonas", "true")).SignInWith.Should().Be(KocSignInSource.Unconfigured);
    }

    [Fact]
    public void A_corrupt_file_falls_back_to_the_wizard_rather_than_failing_to_boot()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path.Combine(_directory, "setup.json"), "{ this is not json");

        Store().SignInWith.Should().Be(KocSignInSource.Unconfigured);
    }

    [Fact]
    public void Reload_picks_up_what_the_other_process_just_wrote()
    {
        var store = Store();
        store.SignInWith.Should().Be(KocSignInSource.Unconfigured);

        // Inside KOC the Web writes this the moment a request arrives already authenticated.
        Store().Save(new KocSetupState { SignInWith = KocSignInSource.KocEnvironment });

        store.Reload().SignInWith.Should().Be(KocSignInSource.KocEnvironment);
    }

    [Fact]
    public void The_wizard_offers_exactly_two_places_to_sign_in()
    {
        KocSignInSourceInfo.All.Should().HaveCount(2);
        KocSignInSourceInfo.All.Select(s => s.Source)
            .Should().BeEquivalentTo([KocSignInSource.KocEnvironment, KocSignInSource.SiteAccounts]);
        KocSignInSourceInfo.All.Should().OnlyContain(s =>
            !string.IsNullOrWhiteSpace(s.DisplayName) && !string.IsNullOrWhiteSpace(s.Summary));
    }
}
