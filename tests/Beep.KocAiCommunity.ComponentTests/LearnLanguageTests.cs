using System.Globalization;
using Beep.KocAiCommunity.Client;
using Beep.KocAiCommunity.Contracts.Learning;
using Beep.KocAiCommunity.Contracts.Localization;
using Beep.KocAiCommunity.Desktop.Local;
using Beep.KocAiCommunity.Web.Components.Pages;
using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using MudBlazor.Services;
using Xunit;

namespace Beep.KocAiCommunity.ComponentTests;

/// <summary>
/// Learning is the half of the platform open to everyone, so it is the half that most needs to be
/// readable in both of KOC's working languages. These check the two halves meet: the interface language
/// chosen in the app bar is also the language the catalogue is asked for, and a track that has no
/// translation is still offered rather than hidden.
/// </summary>
public class LearnLanguageTests : TestContext
{
    private const string ArabicTitle = "ارصد الحالات غير الطبيعية";

    /// <summary>Stands in for the catalogue: English everywhere, with one track translated.</summary>
    private sealed class FakeApi : RemoteFallbackKocApiClient
    {
        public FakeApi() : base(null!) { }

        public string? LastRequestedLanguage { get; private set; }

        public override Task<IReadOnlyList<TrackDto>> GetTracksAsync(string? language = null, CancellationToken ct = default)
        {
            LastRequestedLanguage = language;

            var translated = language == KocLanguages.Arabic
                ? new TrackDto(Guid.NewGuid(), ArabicTitle, "اعثر على القراءات التي لا تنتمي.", "Advanced", 7, "upstream", 8, Language: "ar")
                : new TrackDto(Guid.NewGuid(), "Flag the abnormal", "Find the readings that don't belong.", "Advanced", 7, "upstream", 8, Language: "en");

            return Task.FromResult<IReadOnlyList<TrackDto>>(
            [
                new(Guid.NewGuid(), "Getting started with data", "Read and clean a dataset.", "Beginner", 1, "upstream", 6, Language: "en"),
                translated,
            ]);
        }

        public override Task<IReadOnlyList<MyLearningDto>> GetMyLearningAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<MyLearningDto>>([]);
    }

    /// <summary>
    /// Renders the page as it renders for real: the circuit inherits the culture the request resolved
    /// to, and the page reads it from there rather than owning a toggle of its own.
    /// </summary>
    private (IRenderedComponent<Learn> Page, FakeApi Api) RenderIn(string language)
    {
        Services.AddMudServices();
        Services.AddLogging();
        Services.AddLocalization();
        JSInterop.Mode = JSRuntimeMode.Loose;

        var api = new FakeApi();
        Services.AddSingleton<IKocApiClient>(api);
        Services.AddSingleton(new DevIdentity());

        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo(language);
            return (RenderComponent<Learn>(), api);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    [Fact]
    public void The_page_reads_in_english_by_default()
    {
        var (page, api) = RenderIn(KocLanguages.English);

        api.LastRequestedLanguage.Should().Be(KocLanguages.English);
        page.Markup.Should().Contain("Learning tracks").And.Contain("Flag the abnormal");
    }

    [Fact]
    public void Under_arabic_the_chrome_and_the_catalogue_are_both_arabic()
    {
        // The bug this guards against is a half-translated page: an Arabic lesson under an English
        // heading, or the reverse. One language choice has to reach both.
        var (page, api) = RenderIn(KocLanguages.Arabic);

        api.LastRequestedLanguage.Should().Be(KocLanguages.Arabic, "the catalogue follows the interface");

        page.Markup.Should().Contain("المسارات التعليمية", "the heading comes from the shared resource");
        page.Markup.Should().Contain(ArabicTitle, "and the track comes from the catalogue");
        page.Markup.Should().NotContain("Learning tracks");
    }

    [Fact]
    public void An_arabic_track_renders_right_to_left()
    {
        // Not cosmetic: Arabic prose laid out left-to-right puts punctuation and any embedded Latin
        // term in the wrong place, and the paragraph stops making sense.
        var (page, _) = RenderIn(KocLanguages.Arabic);

        page.Markup.Should().Contain("dir=\"rtl\"");
    }

    [Fact]
    public void A_track_with_no_translation_is_still_offered_and_says_which_language_it_is_in()
    {
        // Hiding the untranslated half would make a partly translated catalogue read as a broken page.
        var (page, _) = RenderIn(KocLanguages.Arabic);

        page.Markup.Should().Contain("Getting started with data");
        page.Markup.Should().Contain("متاح بـEnglish فقط");
    }

    [Fact]
    public void An_english_track_still_reads_left_to_right_inside_an_arabic_page()
    {
        // A track's language is independent of the interface language. The untranslated one must keep
        // its own direction, or its English text mirrors and reads as nonsense.
        var (page, _) = RenderIn(KocLanguages.Arabic);

        page.Markup.Should().Contain("dir=\"ltr\"", "the English track keeps its own direction");
    }
}
