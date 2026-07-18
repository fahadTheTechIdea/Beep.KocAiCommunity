using Beep.KocAiCommunity.Application.Learning;
using Beep.KocAiCommunity.Application.Security;
using Beep.KocAiCommunity.Contracts.Learning;

namespace Beep.KocAiCommunity.Api.Endpoints;

public static class LearningEndpoints
{
    public static RouteGroupBuilder MapLearningEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/tracks", async (IKocCurrentUser me, ILearningService learning, CancellationToken ct) =>
        {
            var tracks = await learning.BrowseVisibleAsync(me.UserId!, ct);
            var result = new List<TrackDto>(tracks.Count);
            foreach (var track in tracks)
            {
                var lessons = await learning.GetLessonsAsync(track.Id, ct);
                result.Add(new TrackDto(track.Id, track.Title, track.Summary, track.Level.ToString(), track.OrderNo, track.Domain, lessons.Count));
            }

            return Results.Ok(result);
        })
        .WithName("BrowseTracks")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        group.MapGet("/tracks/{id:guid}", async (Guid id, ILearningService learning, CancellationToken ct) =>
        {
            var track = await learning.GetAsync(id, ct);
            if (track is null)
            {
                return Results.NotFound();
            }

            var lessons = await learning.GetLessonsAsync(id, ct);
            return Results.Ok(new TrackDetailDto(
                track.Id, track.Title, track.Summary, track.Level.ToString(), track.Domain,
                [.. lessons.Select(l => new LessonDto(l.Id, l.OrderNo, l.Title, l.EstimatedMinutes, l.HandsOnKind, l.Content))]));
        })
        .WithName("GetTrack")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        group.MapPost("/tracks/{id:guid}/enroll", async (Guid id, IKocCurrentUser me, ILearningService learning, CancellationToken ct) =>
        {
            var enrollment = await learning.EnrollAsync(me.UserId!, id, ct);
            return Results.Ok(new EnrollmentDto(enrollment.TrackId, enrollment.Status, enrollment.StartedUtc, enrollment.CompletedUtc));
        })
        .WithName("EnrollInTrack")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        group.MapPost("/tracks/{id:guid}/lessons/{lessonId:guid}/complete", async (Guid id, Guid lessonId, IKocCurrentUser me, ILearningService learning, CancellationToken ct) =>
        {
            await learning.CompleteLessonAsync(me.UserId!, id, lessonId, ct);
            return Results.NoContent();
        })
        .WithName("CompleteLesson")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        group.MapGet("/me/learning", async (IKocCurrentUser me, ILearningService learning, CancellationToken ct) =>
        {
            var items = await learning.GetMyLearningAsync(me.UserId!, ct);
            return Results.Ok(items
                .Select(i => new MyLearningDto(i.Enrollment.TrackId, i.Title, i.Enrollment.Status, i.CompletedLessons, i.TotalLessons))
                .ToList());
        })
        .WithName("MyLearning")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        return group;
    }
}
