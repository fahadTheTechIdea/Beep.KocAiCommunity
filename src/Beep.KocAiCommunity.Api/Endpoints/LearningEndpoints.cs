using Beep.KocAiCommunity.Application.Authorization;
using Beep.KocAiCommunity.Application.Competitions;
using Beep.KocAiCommunity.Application.Learning;
using Beep.KocAiCommunity.Application.Security;
using Beep.KocAiCommunity.Contracts.Learning;
using Beep.KocAiCommunity.Domain.Learning;

namespace Beep.KocAiCommunity.Api.Endpoints;

public static class LearningEndpoints
{
    public static RouteGroupBuilder MapLearningEndpoints(this RouteGroupBuilder group)
    {
        // Browsing the catalogue is open to everyone. Learning what the platform teaches is the reason
        // someone signs in — putting it behind sign-in asks people to commit before they can see what
        // they are committing to. Visibility still applies: an anonymous reader resolves to no org
        // membership, so they see company-wide tracks and nothing narrower.
        group.MapGet("/tracks", async (
            string? language, IKocCurrentUser me, ILearningService learning, ICompetitionService competitions, CancellationToken ct) =>
        {
            var tracks = await learning.BrowseVisibleAsync(me.UserId ?? string.Empty, TrackLanguages.Normalize(language), ct);

            // Titles for the linked competitions, so a track can say where it leads without the page
            // making a request per card. A link into a hidden competition resolves to nothing and the
            // track simply shows no destination.
            var visible = (await competitions.BrowseVisibleAsync(me.UserId ?? string.Empty, ct))
                .ToDictionary(c => c.Id, c => c.Title);

            var result = new List<TrackDto>(tracks.Count);
            foreach (var track in tracks)
            {
                var lessons = await learning.GetLessonsAsync(track.Id, ct);
                var linked = track.RecommendedCompetitionId is { } id && visible.TryGetValue(id, out var title)
                    ? (Id: (Guid?)id, Title: title)
                    : (Id: null, Title: null);

                result.Add(new TrackDto(
                    track.Id, track.Title, track.Summary, track.Level.ToString(), track.OrderNo, track.Domain,
                    lessons.Count, linked.Id, linked.Title, track.Language));
            }

            return Results.Ok(result);
        })
        .WithName("BrowseTracks")
        .AllowAnonymous();

        group.MapGet("/tracks/{id:guid}", async (
            Guid id, IKocCurrentUser me, ILearningService learning, IVisibilityEvaluator visibility, CancellationToken ct) =>
        {
            var track = await learning.GetAsync(id, ct);

            // The scope check belongs here, not only on the listing: fetching by id used to return any
            // track to any signed-in caller, so a narrower-than-company track leaked to anyone who knew
            // its id. "Not visible" answers the same as "not there" — no probing for what exists.
            if (track is null || !await visibility.CanSeeAsync(me.UserId ?? string.Empty, track.VisibilityScope, track.VisibilityOrgUnitId, ct))
            {
                return Results.NotFound();
            }

            var lessons = await learning.GetLessonsAsync(id, ct);
            var translations = await learning.GetTranslationsAsync(id, ct);
            return Results.Ok(new TrackDetailDto(
                track.Id, track.Title, track.Summary, track.Level.ToString(), track.Domain,
                [.. lessons.Select(l => new LessonDto(l.Id, l.OrderNo, l.Title, l.EstimatedMinutes, l.HandsOnKind, l.Content))],
                track.Language, translations));
        })
        .WithName("GetTrack")
        .AllowAnonymous();

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
