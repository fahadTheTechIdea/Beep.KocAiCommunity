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

    private static async Task<Guid> FirstTrackId(HttpClient client)
    {
        var tracks = (await client.GetFromJsonAsync<List<TrackDto>>("/api/v1/tracks"))!;
        return tracks.First(t => t.Title == "Getting started with data").Id;
    }
}
