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
    public async Task Tracks_require_authentication()
    {
        var client = _factory.CreateClientAs(sub: null);
        (await client.GetAsync("/api/v1/tracks")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Browse_returns_the_starter_tracks()
    {
        var client = _factory.CreateClientAs("learner-browse", "Employee");
        var tracks = (await client.GetFromJsonAsync<List<TrackDto>>("/api/v1/tracks"))!;

        tracks.Should().HaveCount(4);
        tracks.Select(t => t.Title).Should().Contain("AI for Everyone — Start Here")
            .And.Contain("Getting started with data");
        tracks.First(t => t.Title == "Getting started with data").LessonCount.Should().Be(6);
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
