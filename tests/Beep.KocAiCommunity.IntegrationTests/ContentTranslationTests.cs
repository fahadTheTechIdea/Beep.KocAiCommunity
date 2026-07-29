using System.Net.Http.Headers;
using System.Net.Http.Json;
using Beep.KocAiCommunity.Contracts.Competitions;
using Beep.KocAiCommunity.Contracts.Engagement;
using FluentAssertions;
using Xunit;

namespace Beep.KocAiCommunity.IntegrationTests;

/// <summary>
/// The content KOC ships — competition categories, badges — served in the caller's language.
/// <para>
/// Interface strings are keyed on their English in a .resx, which works because changing that English
/// is a code change. These are database rows an administrator can rename, so they are keyed on the
/// row's code instead. These pin both halves: the translation is served, and the fallback is English
/// rather than a blank.
/// </para>
/// </summary>
public class ContentTranslationTests(KocApiFactory factory) : IClassFixture<KocApiFactory>
{
    private readonly KocApiFactory _factory = factory;

    private HttpClient Client(string? language)
    {
        var client = _factory.CreateClientAs("translation-reader", "Employee");
        if (language is not null)
        {
            client.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue(language));
        }

        return client;
    }

    [Fact]
    public async Task Competition_categories_are_served_in_the_language_the_caller_asks_for()
    {
        var english = (await Client(null).GetFromJsonAsync<List<CompetitionCategoryDto>>("/api/v1/competitions/categories"))!;
        var arabic = (await Client("ar").GetFromJsonAsync<List<CompetitionCategoryDto>>("/api/v1/competitions/categories"))!;

        english.Should().Contain(c => c.Code == "subsurface" && c.Name == "Subsurface");
        arabic.Should().Contain(c => c.Code == "subsurface" && c.Name == "المكامن");

        // The code is what everything else references — translating a name must not change identity.
        arabic.Select(c => c.Code).Should().BeEquivalentTo(english.Select(c => c.Code));

        // HSE is a department's name, not a phrase. It reads the same in both.
        arabic.Single(c => c.Code == "hse").Name.Should().Be("HSE");
    }

    [Fact]
    public async Task Badges_are_served_in_the_language_the_caller_asks_for()
    {
        var arabic = (await Client("ar").GetFromJsonAsync<List<BadgeDto>>("/api/v1/engagement/badges/catalog"))!;

        var gusher = arabic.Single(b => b.Code == "competition-winner");
        gusher.Name.Should().Be("فوّارة", "the English is an oilfield metaphor, and so is the Arabic");
        gusher.Description.Should().Be("حللت أولًا في مسابقة.");
    }

    [Fact]
    public async Task An_unknown_language_falls_back_to_english_rather_than_blank()
    {
        var categories = (await Client("fr").GetFromJsonAsync<List<CompetitionCategoryDto>>("/api/v1/competitions/categories"))!;

        categories.Should().Contain(c => c.Code == "subsurface" && c.Name == "Subsurface");
        categories.Should().OnlyContain(c => !string.IsNullOrWhiteSpace(c.Name));
    }

    [Fact]
    public async Task Every_seeded_category_and_badge_has_arabic()
    {
        // The seeder is hand-written, so adding a category or a badge without a translation is easy to
        // do and impossible to notice — the English simply keeps showing. This is what notices.
        var categories = (await Client("ar").GetFromJsonAsync<List<CompetitionCategoryDto>>("/api/v1/competitions/categories"))!;
        var badges = (await Client("ar").GetFromJsonAsync<List<BadgeDto>>("/api/v1/engagement/badges/catalog"))!;

        // "HSE" is deliberately identical in both, so it is excluded rather than special-cased away.
        var untranslatedCategories = categories
            .Where(c => c.Code != "hse")
            .Where(c => c.Name.All(ch => ch < 0x0600))
            .Select(c => c.Code)
            .ToList();

        var untranslatedBadges = badges
            .Where(b => b.Name.All(ch => ch < 0x0600))
            .Select(b => b.Code)
            .ToList();

        untranslatedCategories.Should().BeEmpty("every seeded category needs a line in ContentTranslationSeeder");
        untranslatedBadges.Should().BeEmpty("every badge in BadgeCatalog needs a line in ContentTranslationSeeder");
    }
}
