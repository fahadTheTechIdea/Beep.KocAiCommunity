using Beep.KocAiCommunity.ServiceDefaults.Security;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Beep.KocAiCommunity.UnitTests;

/// <summary>
/// The first-run setup store: what the wizard writes, what a configured deployment overrides, and the
/// rule that an unreadable file must never stop the app from booting into the wizard that rewrites it.
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
    public void A_fresh_install_is_unconfigured_so_the_wizard_runs()
    {
        var store = Store();

        store.IsConfigured.Should().BeFalse();
        store.Mode.Should().Be(KocAuthMode.Unconfigured);
    }

    [Fact]
    public void Saving_a_choice_persists_it_for_the_other_process_to_read()
    {
        var store = Store();
        store.Save(new KocSetupState { Mode = KocAuthMode.LocalAccounts });

        // A second store over the same file is the API reading what the Web wrote.
        var reread = Store();
        reread.Mode.Should().Be(KocAuthMode.LocalAccounts);
        reread.IsConfigured.Should().BeTrue();
        reread.Current.CompletedUtc.Should().NotBeNull();
    }

    [Fact]
    public void Local_accounts_get_a_signing_key_both_hosts_share()
    {
        var saved = Store().Save(new KocSetupState { Mode = KocAuthMode.LocalAccounts });

        saved.TokenSigningKey.Should().NotBeNullOrWhiteSpace("the API signs tokens with it and must not invent one per process");
        Convert.FromBase64String(saved.TokenSigningKey!).Should().HaveCount(32);
        Store().Current.TokenSigningKey.Should().Be(saved.TokenSigningKey);
    }

    [Fact]
    public void Modes_without_passwords_need_no_signing_key()
    {
        Store().Save(new KocSetupState { Mode = KocAuthMode.WindowsIntranet })
            .TokenSigningKey.Should().BeNull();
    }

    [Fact]
    public void An_explicit_configured_mode_wins_over_the_file()
    {
        Store().Save(new KocSetupState { Mode = KocAuthMode.DemoPersonas });

        // A deployment that states its mode in configuration is already set up and never sees the wizard,
        // whatever a stale file on that machine happens to say.
        Store(("Auth:Mode", "LocalAccounts")).Mode.Should().Be(KocAuthMode.LocalAccounts);
    }

    [Theory]
    [InlineData("AzureAd:TenantId", "tenant", "AzureAd:ClientId", "client", KocAuthMode.EntraId)]
    [InlineData("WindowsAuth:Enabled", "true", "Unused", null, KocAuthMode.WindowsIntranet)]
    [InlineData("DevAuth:Enabled", "true", "Unused", null, KocAuthMode.DemoPersonas)]
    public void Deployments_configured_before_the_wizard_existed_keep_working(
        string key1, string? value1, string key2, string? value2, KocAuthMode expected)
    {
        Store((key1, value1), (key2, value2)).Mode.Should().Be(expected);
    }

    [Fact]
    public void A_corrupt_file_falls_back_to_the_wizard_rather_than_failing_to_boot()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path.Combine(_directory, "setup.json"), "{ this is not json");

        Store().Mode.Should().Be(KocAuthMode.Unconfigured);
    }

    [Fact]
    public void Reload_picks_up_what_the_wizard_just_wrote()
    {
        var store = Store();
        store.Mode.Should().Be(KocAuthMode.Unconfigured);

        // Simulates the other process writing the file after this one already answered "unconfigured".
        Store().Save(new KocSetupState { Mode = KocAuthMode.LocalAccounts });

        store.Reload().Mode.Should().Be(KocAuthMode.LocalAccounts);
    }

    [Fact]
    public void Every_mode_the_wizard_offers_can_be_described()
    {
        // The wizard renders one card per option, so a mode with no description would render blank.
        KocAuthModeInfo.All.Should().HaveCount(4);
        KocAuthModeInfo.All.Should().OnlyContain(m =>
            !string.IsNullOrWhiteSpace(m.DisplayName) && !string.IsNullOrWhiteSpace(m.Summary));
        KocAuthModeInfo.All.Select(m => m.Mode).Should().NotContain(KocAuthMode.Unconfigured);
    }
}
