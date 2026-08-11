using System.Net;
using System.Net.Http.Json;
using Beep.KocAiCommunity.Contracts.Competitions;
using FluentAssertions;
using Xunit;

namespace Beep.KocAiCommunity.IntegrationTests;

/// <summary>
/// A competition is written by a colleague, not shipped with the platform, so its words cannot live in
/// a resource file. These hold the content-translation path end to end: an author writes the challenge
/// in both languages, an Arabic reader sees theirs, and an untranslated one still reads — in English —
/// rather than showing a blank card.
/// </summary>
public class CompetitionTranslationTests(KocApiFactory factory) : IClassFixture<KocApiFactory>
{
    private readonly KocApiFactory _factory = factory;

    private static HttpRequestMessage Get(string url, string? language = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (language is not null)
        {
            request.Headers.Add("Accept-Language", language);
        }

        return request;
    }

    private static async Task<CompetitionDto> CreateAsync(HttpClient client, string title, string? titleAr, string? descriptionAr)
    {
        var response = await client.PostAsJsonAsync("/api/v1/competitions", new CreateCompetitionRequest(
            title, "Predict the thing that matters.", "Company", null, null, 5, "accuracy",
            TitleAr: titleAr, DescriptionAr: descriptionAr));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<CompetitionDto>())!;
    }

    private static async Task<CompetitionDto> ReadAsync(HttpClient client, Guid id, string? language)
    {
        var response = await client.SendAsync(Get($"/api/v1/competitions/{id}", language));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<CompetitionDto>())!;
    }

    [Fact]
    public async Task An_author_writes_both_languages_and_each_reader_gets_theirs()
    {
        var author = _factory.CreateClientAs("tr-author", "Employee", "PlatformAdmin");

        var created = await CreateAsync(author, "Pump failure prediction",
            titleAr: "التنبؤ بأعطال المضخّات",
            descriptionAr: "توقّع أي مضخّة ستتعطّل الشهر القادم.");

        // The creating call answers in the author's own language, which is English here.
        created.Title.Should().Be("Pump failure prediction");

        (await ReadAsync(author, created.Id, "en")).Title.Should().Be("Pump failure prediction");

        var arabic = await ReadAsync(author, created.Id, "ar");
        arabic.Title.Should().Be("التنبؤ بأعطال المضخّات");
        arabic.Description.Should().Be("توقّع أي مضخّة ستتعطّل الشهر القادم.");
    }

    [Fact]
    public async Task An_untranslated_competition_reads_in_english_rather_than_blank()
    {
        var author = _factory.CreateClientAs("tr-author-2", "Employee", "PlatformAdmin");

        // No Arabic supplied at all — the common case, and it must not produce an empty card.
        var created = await CreateAsync(author, "Facilities throughput", titleAr: null, descriptionAr: null);

        var arabic = await ReadAsync(author, created.Id, "ar");
        arabic.Title.Should().Be("Facilities throughput");
        arabic.Description.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task The_browse_list_is_translated_too_not_only_the_detail_page()
    {
        var author = _factory.CreateClientAs("tr-author-3", "Employee", "PlatformAdmin");
        var created = await CreateAsync(author, "Well integrity check", "فحص سلامة الآبار", "وصف بالعربية.");

        var response = await author.SendAsync(Get("/api/v1/competitions", "ar"));
        var list = await response.Content.ReadFromJsonAsync<List<CompetitionDto>>();

        list!.Single(c => c.Id == created.Id).Title.Should().Be("فحص سلامة الآبار");
    }

    [Fact]
    public async Task Editing_a_translation_replaces_it_and_clearing_it_falls_back()
    {
        var author = _factory.CreateClientAs("tr-author-4", "Employee", "PlatformAdmin");
        var created = await CreateAsync(author, "Corrosion risk", "خطر التآكل", "وصف أولي.");

        // Replace.
        (await author.PutAsJsonAsync($"/api/v1/competitions/{created.Id}/translations",
            new SetCompetitionTranslationRequest("ar", "مخاطر التآكل", "وصف محدّث.")))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await ReadAsync(author, created.Id, "ar")).Title.Should().Be("مخاطر التآكل");

        // Blank clears it: the challenge falls back to the author's English rather than rendering empty.
        (await author.PutAsJsonAsync($"/api/v1/competitions/{created.Id}/translations",
            new SetCompetitionTranslationRequest("ar", "", "")))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await ReadAsync(author, created.Id, "ar")).Title.Should().Be("Corrosion risk");
    }

    [Fact]
    public async Task Someone_elses_competition_cannot_be_put_words_into()
    {
        var author = _factory.CreateClientAs("tr-owner", "Employee", "PlatformAdmin");
        var created = await CreateAsync(author, "Not yours", null, null);

        var stranger = _factory.CreateClientAs("tr-stranger", "Employee");

        (await stranger.PutAsJsonAsync($"/api/v1/competitions/{created.Id}/translations",
            new SetCompetitionTranslationRequest("ar", "عنوان مزيّف", null)))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task The_competitions_that_ship_with_the_platform_are_already_in_arabic()
    {
        // The seeded set is what an Arabic-speaking colleague meets on their first visit. Translating
        // the interface but leaving fifteen English challenge titles in the arena would be the most
        // visible way to be half-bilingual.
        var reader = _factory.CreateClientAs("tr-reader", "Employee");

        var english = await (await reader.SendAsync(Get("/api/v1/competitions", "en")))
            .Content.ReadFromJsonAsync<List<CompetitionDto>>();
        var arabic = await (await reader.SendAsync(Get("/api/v1/competitions", "ar")))
            .Content.ReadFromJsonAsync<List<CompetitionDto>>();

        arabic!.Should().HaveCount(english!.Count);

        // Counted, not asserted over every row: this fixture's database is shared with the sibling
        // tests above, which create their own English-only competitions. The claim is about the set
        // that ships with the platform, so the floor is what that set contains.
        var translated = arabic
            .Where(ar => english.Single(en => en.Id == ar.Id) is var en
                         && ar.Title != en.Title
                         && ar.Description != en.Description
                         && ar.Title.Any(ch => ch >= '؀' && ch <= 'ۿ'))
            .ToList();

        translated.Should().HaveCountGreaterThanOrEqualTo(15,
            "every competition the platform ships with should read in Arabic");
    }

    [Fact]
    public async Task The_landing_page_showcase_is_translated_too()
    {
        // The showcase feeds the home-page hero — the featured competition an arriving visitor reads
        // before anything else. It was building its DTOs on its own and missed the translation the
        // browse list already had, so the page switched to Arabic around a competition that did not.
        var reader = _factory.CreateClientAs("tr-showcase", "Employee");

        var english = await (await reader.SendAsync(Get("/api/v1/public/showcase", "en")))
            .Content.ReadFromJsonAsync<Contracts.Competitions.PublicShowcaseDto>();
        var arabic = await (await reader.SendAsync(Get("/api/v1/public/showcase", "ar")))
            .Content.ReadFromJsonAsync<Contracts.Competitions.PublicShowcaseDto>();

        arabic!.Competitions.Should().NotBeEmpty();

        var translated = arabic.Competitions
            .Where(ar => english!.Competitions.Single(en => en.Id == ar.Id).Title != ar.Title
                         && ar.Title.Any(ch => ch >= '؀' && ch <= 'ۿ'))
            .ToList();

        translated.Should().HaveCountGreaterThanOrEqualTo(1,
            "the landing page must not show an English competition inside an Arabic page");
    }

    [Fact]
    public async Task English_is_the_competition_itself_not_a_translation_of_it()
    {
        var author = _factory.CreateClientAs("tr-author-5", "Employee", "PlatformAdmin");
        var created = await CreateAsync(author, "English only", null, null);

        // Accepting this would put the title in two places and let them disagree.
        (await author.PutAsJsonAsync($"/api/v1/competitions/{created.Id}/translations",
            new SetCompetitionTranslationRequest("en", "Renamed via translations", null)))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
