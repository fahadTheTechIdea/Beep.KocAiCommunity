using System.Net;
using System.Net.Http.Json;
using Beep.KocAiCommunity.Contracts.Learning;
using FluentAssertions;
using Xunit;

namespace Beep.KocAiCommunity.IntegrationTests;

public class LearningEndpointsTests(KocApiFactory factory) : IClassFixture<KocApiFactory>
{
    private readonly KocApiFactory _factory = factory;

    [Fact]
    public async Task Anyone_can_read_the_catalogue_without_signing_in()
    {
        // Learning is the reason someone comes to the platform. Putting the material behind sign-in asks
        // people to commit before they can see what they are committing to.
        var guest = _factory.CreateClientAs(sub: null);

        var tracks = (await guest.GetFromJsonAsync<List<TrackDto>>("/api/v1/tracks"))!;
        tracks.Should().NotBeEmpty();

        // And the lessons inside a track, not just its title.
        var detail = (await guest.GetFromJsonAsync<TrackDetailDto>($"/api/v1/tracks/{tracks[0].Id}"))!;
        detail.Lessons.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Reading_is_open_but_recording_progress_still_needs_a_person()
    {
        // Enrolling and completing write progress against a user, so they are the one part that cannot
        // be anonymous — there is nobody to record it for.
        var guest = _factory.CreateClientAs(sub: null);
        var tracks = (await guest.GetFromJsonAsync<List<TrackDto>>("/api/v1/tracks"))!;

        (await guest.PostAsync($"/api/v1/tracks/{tracks[0].Id}/enroll", null))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await guest.GetAsync("/api/v1/me/learning"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Browse_returns_the_starter_tracks()
    {
        var client = _factory.CreateClientAs("learner-browse", "Employee");
        var tracks = (await client.GetFromJsonAsync<List<TrackDto>>("/api/v1/tracks"))!;

        // Not an exact count — the catalogue grows as tracks are authored, and pinning the number here
        // would make every new track look like a regression.
        tracks.Select(t => t.Title).Should().Contain("AI for Everyone — Start Here")
            .And.Contain("Getting started with data");
        tracks.First(t => t.Title == "Getting started with data").LessonCount.Should().Be(6);

        // The authored tracks reach the catalogue too, with their lessons.
        tracks.Select(t => t.Title).Should().Contain("Prepare the data").And.Contain("Evaluate honestly");
        tracks.Should().OnlyContain(t => t.LessonCount > 0);
    }

    [Fact]
    public async Task Enroll_is_idempotent()
    {
        var client = _factory.CreateClientAs("learner-enroll", "Employee");
        var trackId = await FirstTrackId(client);

        await client.PostAsync($"/api/v1/tracks/{trackId}/enroll", null);
        await client.PostAsync($"/api/v1/tracks/{trackId}/enroll", null);

        var mine = (await client.GetFromJsonAsync<List<MyLearningDto>>("/api/v1/me/learning"))!;
        mine.Should().ContainSingle(m => m.TrackId == trackId);
    }

    [Fact]
    public async Task Completing_every_lesson_completes_the_track()
    {
        var client = _factory.CreateClientAs("learner-finish", "Employee");
        var trackId = await FirstTrackId(client);

        var detail = (await client.GetFromJsonAsync<TrackDetailDto>($"/api/v1/tracks/{trackId}"))!;
        detail.Lessons.Should().NotBeEmpty();

        foreach (var lesson in detail.Lessons)
        {
            var response = await client.PostAsync($"/api/v1/tracks/{trackId}/lessons/{lesson.Id}/complete", null);
            response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        var mine = (await client.GetFromJsonAsync<List<MyLearningDto>>("/api/v1/me/learning"))!;
        var entry = mine.Single(m => m.TrackId == trackId);
        entry.CompletedLessons.Should().Be(entry.TotalLessons);
        entry.Status.Should().Be("completed");
    }

    [Fact]
    public async Task The_catalogue_reads_in_arabic_without_hiding_what_is_only_in_english()
    {
        // Learning is the half of the platform open to everyone, so it is the half that most needs to be
        // readable in both of KOC's working languages.
        var guest = _factory.CreateClientAs(sub: null);

        var english = (await guest.GetFromJsonAsync<List<TrackDto>>("/api/v1/tracks"))!;
        var arabic = (await guest.GetFromJsonAsync<List<TrackDto>>("/api/v1/tracks?language=ar"))!;

        // The translated track swaps for its Arabic version...
        english.Should().Contain(t => t.Title == "Flag the abnormal");
        arabic.Should().Contain(t => t.Title == "اكتشاف الشاذ");
        arabic.Should().NotContain(t => t.Title == "Flag the abnormal", "the two are the same material");

        // ...and it takes the original's place rather than being appended to the end.
        arabic.Should().HaveSameCount(english, "a translation replaces its original, it does not add to the catalogue");

        // ...while everything not yet translated is still offered, marked as English. A partly
        // translated catalogue that hid its untranslated half would read as a broken page.
        arabic.Should().Contain(t => t.Title == "Getting started with data" && t.Language == "en");
        arabic.Single(t => t.Title == "اكتشاف الشاذ").Language.Should().Be("ar");
    }

    [Fact]
    public async Task A_track_says_which_languages_it_is_published_in()
    {
        var guest = _factory.CreateClientAs(sub: null);
        var arabic = (await guest.GetFromJsonAsync<List<TrackDto>>("/api/v1/tracks?language=ar"))!;
        var translated = arabic.Single(t => t.Title == "اكتشاف الشاذ");

        // Read in either language, a track offers the other — the reader who lands on the wrong one
        // needs a way across, and the API is where that pairing has to be answered.
        var detail = (await guest.GetFromJsonAsync<TrackDetailDto>($"/api/v1/tracks/{translated.Id}"))!;
        detail.Language.Should().Be("ar");
        detail.Translations.Should().ContainKey("en");

        var original = (await guest.GetFromJsonAsync<TrackDetailDto>($"/api/v1/tracks/{detail.Translations!["en"]}"))!;
        original.Title.Should().Be("Flag the abnormal");
        original.Translations.Should().ContainKey("ar").WhoseValue.Should().Be(translated.Id);
    }

    [Fact]
    public async Task An_unrecognised_language_reads_as_english_rather_than_empty()
    {
        var guest = _factory.CreateClientAs(sub: null);

        var tracks = (await guest.GetFromJsonAsync<List<TrackDto>>("/api/v1/tracks?language=zz"))!;

        tracks.Should().NotBeEmpty("a bad query string must not empty the catalogue");
        tracks.Should().Contain(t => t.Title == "Flag the abnormal");
    }

    private static async Task<Guid> FirstTrackId(HttpClient client)
    {
        var tracks = (await client.GetFromJsonAsync<List<TrackDto>>("/api/v1/tracks"))!;
        return tracks.First(t => t.Title == "Getting started with data").Id;
    }
}
