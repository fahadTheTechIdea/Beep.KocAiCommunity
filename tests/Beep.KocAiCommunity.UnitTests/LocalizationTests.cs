using System.Globalization;
using Beep.KocAiCommunity.Contracts.Localization;
using Beep.KocAiCommunity.Domain.Learning;
using Beep.KocAiCommunity.Ui.Shared.Localization;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Xunit;

namespace Beep.KocAiCommunity.UnitTests;

/// <summary>
/// The interface speaks English and Arabic. These pin the parts that fail silently: a resource that
/// doesn't resolve shows English forever with no error, and a formatting culture that follows the
/// interface language changes every number on the page without anyone asking it to.
/// </summary>
public class LocalizationTests
{
    private static IStringLocalizer<Strings> Localizer()
    {
        var services = new ServiceCollection();
        services.AddLogging();          // the resource factory logs a miss; the host already has this
        services.AddLocalization();
        return services.BuildServiceProvider().GetRequiredService<IStringLocalizer<Strings>>();
    }

    private static T InCulture<T>(string language, Func<T> read)
    {
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo(language);
            return read();
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    [Fact]
    public void The_arabic_resource_actually_resolves()
    {
        // The whole scheme rests on this. Resource lookup is by convention — Strings.cs beside
        // Strings.resx, same namespace, no ResourcesPath — and when the convention is broken nothing
        // throws: every string quietly renders English and the app looks like it was never translated.
        var localizer = Localizer();

        InCulture(KocLanguages.Arabic, () => localizer["Learning tracks"].Value)
            .Should().Be("المسارات التعليمية");
    }

    [Fact]
    public void An_untranslated_string_falls_back_to_its_english_key()
    {
        // This is what makes a gradual rollout safe: a string not yet in the Arabic resource renders
        // correct English rather than a raw identifier. LocalizationCoverageTests is what stops that
        // fallback from becoming the permanent state.
        var localizer = Localizer();

        var missing = InCulture(KocLanguages.Arabic, () => localizer["A string nobody has translated yet"]);

        missing.Value.Should().Be("A string nobody has translated yet");
        missing.ResourceNotFound.Should().BeTrue();
    }

    [Fact]
    public void English_reads_as_written_without_a_neutral_resource_entry()
    {
        // Strings.resx is deliberately empty — the English text is the key, so English needs no entries.
        var localizer = Localizer();

        InCulture(KocLanguages.English, () => localizer["Learning tracks"].Value).Should().Be("Learning tracks");
    }

    [Fact]
    public void Placeholders_survive_translation()
    {
        var localizer = Localizer();

        InCulture(KocLanguages.Arabic, () => localizer["{0} lessons", 8].Value).Should().Be("8 دروس");
    }

    [Fact]
    public void Numbers_and_dates_do_not_change_with_the_interface_language()
    {
        // Only the words change. An AUC of 0.93 pasted into a chat has to read the same to the colleague
        // who set the site to Arabic — Eastern Arabic numerals here would be a wrong-number bug report,
        // not a feature. KocLanguages.FormattingCulture is what the request pipeline pins CurrentCulture to.
        var formatting = new CultureInfo(KocLanguages.FormattingCulture);

        0.9345.ToString("0.###", formatting).Should().Be("0.935");
        1234.5.ToString("N1", formatting).Should().Be("1,234.5");
        new DateTime(2026, 7, 29, 0, 0, 0, DateTimeKind.Utc).ToString("d", formatting).Should().Be("29/07/2026");
    }

    [Fact]
    public void The_interface_languages_and_the_content_languages_agree()
    {
        // Two layers name the same two languages: KocLanguages in Contracts for the interface, and
        // TrackLanguages in the domain for authored content. They cannot reference each other without
        // inverting the dependency direction, so this is what keeps them from drifting apart.
        KocLanguages.All.Should().BeEquivalentTo(TrackLanguages.All);

        foreach (var language in KocLanguages.All)
        {
            KocLanguages.NativeName(language).Should().Be(TrackLanguages.NativeName(language));
            KocLanguages.IsRightToLeft(language).Should().Be(TrackLanguages.IsRightToLeft(language));
            KocLanguages.Normalize(language).Should().Be(TrackLanguages.Normalize(language));
        }
    }

    [Theory]
    [InlineData("ar", "rtl")]
    [InlineData("en", "ltr")]
    [InlineData("zz", "ltr")]
    [InlineData(null, "ltr")]
    public void Direction_follows_the_language(string? language, string expected) =>
        KocLanguages.Direction(KocLanguages.Normalize(language)).Should().Be(expected);
}
