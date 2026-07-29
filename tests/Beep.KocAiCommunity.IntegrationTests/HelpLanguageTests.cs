using System.Net.Http.Headers;
using System.Net.Http.Json;
using Beep.KocAiCommunity.Contracts.Help;
using FluentAssertions;
using Xunit;

namespace Beep.KocAiCommunity.IntegrationTests;

/// <summary>
/// The help section, in the reader's language where it has been translated.
/// <para>
/// Only the seven general articles are translated. The nine node-and-algorithm reference pages stay
/// English on purpose: they describe node labels that are themselves still English, and translating the
/// description of an English thing helps nobody. These pin that the split behaves — translated where it
/// should be, English where it should be, and never missing.
/// </para>
/// </summary>
public class HelpLanguageTests(KocApiFactory factory) : IClassFixture<KocApiFactory>
{
    private readonly KocApiFactory _factory = factory;

    private HttpClient Client(string? language)
    {
        var client = _factory.CreateClientAs("help-reader", "Employee");
        if (language is not null)
        {
            client.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue(language));
        }

        return client;
    }

    [Fact]
    public async Task A_translated_article_is_served_in_arabic()
    {
        var article = await Client("ar").GetFromJsonAsync<HelpArticleDto>("/api/v1/help/articles/getting-started");

        article!.Title.Should().Be("البداية");
        article.BodyMarkdown.Should().Contain("أهلًا بك");
        article.BodyMarkdown.Should().NotContain("Welcome to KOC AI Community");
    }

    [Fact]
    public async Task The_same_article_is_english_by_default()
    {
        var article = await Client(null).GetFromJsonAsync<HelpArticleDto>("/api/v1/help/articles/getting-started");

        article!.Title.Should().Be("Getting started");
        article.BodyMarkdown.Should().Contain("Welcome to KOC AI Community");
    }

    [Fact]
    public async Task An_untranslated_article_stays_english_rather_than_disappearing()
    {
        // The node reference is deliberately not translated. It must still be there and still readable.
        var article = await Client("ar").GetFromJsonAsync<HelpArticleDto>("/api/v1/help/articles/nodes-source");

        article!.Title.Should().Be("Nodes: Source");
        article.BodyMarkdown.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task The_arabic_listing_holds_every_article_the_english_one_does()
    {
        var english = (await Client(null).GetFromJsonAsync<List<HelpArticleSummaryDto>>("/api/v1/help/articles"))!;
        var arabic = (await Client("ar").GetFromJsonAsync<List<HelpArticleSummaryDto>>("/api/v1/help/articles"))!;

        arabic.Select(a => a.Slug).Should().BeEquivalentTo(english.Select(a => a.Slug),
            "translating an article must not add or remove one");

        arabic.Should().Contain(a => a.Slug == "faq" && a.Title == "الأسئلة الشائعة");
    }

    [Fact]
    public async Task Category_filtering_still_works_in_arabic()
    {
        // The category is a shared key, not prose — an Arabic article carries the English category so a
        // reader can still filter by it. Translating it would have split the catalogue in two.
        var arabic = (await Client("ar").GetFromJsonAsync<List<HelpArticleSummaryDto>>("/api/v1/help/articles?category=Basics"))!;

        arabic.Should().NotBeEmpty();
        arabic.Should().Contain(a => a.Slug == "getting-started" && a.Title == "البداية");
        arabic.Should().OnlyContain(a => a.Category == "Basics");
    }
}
