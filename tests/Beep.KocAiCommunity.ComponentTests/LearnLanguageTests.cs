using Beep.KocAiCommunity.Client;
using Beep.KocAiCommunity.Contracts.Learning;
using Beep.KocAiCommunity.Desktop.Local;
using Beep.KocAiCommunity.Web.Components.Pages;
using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using Xunit;

namespace Beep.KocAiCommunity.ComponentTests;

/// <summary>
/// Learning is the half of the platform open to everyone, so it is the half that most needs to be
/// readable in both of KOC's working languages. These check the reader can actually get to the Arabic —
/// the API can serve a translation perfectly and it counts for nothing if no control reaches it.
/// </summary>
public class LearnLanguageTests : TestContext
{
    private const string ArabicTitle = "اكتشاف الشاذ";

    /// <summary>Stands in for the catalogue: English everywhere, with one track translated.</summary>
    private sealed class FakeApi : RemoteFallbackKocApiClient
    {
        public FakeApi() : base(null!) { }

        public string? LastRequestedLanguage { get; private set; }

        public override Task<IReadOnlyList<TrackDto>> GetTracksAsync(string? language = null, CancellationToken ct = default)
        {
            LastRequestedLanguage = language;

            var translated = language == "ar"
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

    private (IRenderedComponent<Learn> Page, FakeApi Api) Render()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;

        var api = new FakeApi();
        Services.AddSingleton<IKocApiClient>(api);
        Services.AddSingleton(new DevIdentity());

        return (RenderComponent<Learn>(), api);
    }

    [Fact]
    public void The_page_opens_in_english_and_offers_arabic_by_name()
    {
        var (page, api) = Render();

        api.LastRequestedLanguage.Should().Be("en");
        page.Markup.Should().Contain("Flag the abnormal");

        // Each language is named in itself. Someone looking for Arabic looks for العربية, not for the
        // word "Arabic" written in the script they came here to leave.
        page.Markup.Should().Contain("العربية").And.Contain("English");
    }

    [Fact]
    public void Choosing_arabic_reloads_the_catalogue_in_arabic_and_reads_right_to_left()
    {
        var (page, api) = Render();

        var arabicButton = page.FindAll("button").Single(b => b.TextContent.Contains("العربية"));
        arabicButton.Click();

        api.LastRequestedLanguage.Should().Be("ar");
        page.Markup.Should().Contain(ArabicTitle).And.NotContain("Flag the abnormal");

        // Direction is not cosmetic: Arabic prose rendered left-to-right puts punctuation and any
        // embedded Latin term in the wrong place, and the paragraph stops making sense.
        page.Markup.Should().Contain("dir=\"rtl\"");
    }

    [Fact]
    public void A_track_with_no_translation_is_still_offered_and_says_which_language_it_is_in()
    {
        var (page, _) = Render();

        page.FindAll("button").Single(b => b.TextContent.Contains("العربية")).Click();

        // Hiding the untranslated half would make a partly translated catalogue read as a broken page.
        page.Markup.Should().Contain("Getting started with data");
        page.Markup.Should().Contain("Only in English");
    }
}
