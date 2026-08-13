using Beep.KocAiCommunity.Application.Authorization;
using Beep.KocAiCommunity.Application.Competitions;
using Beep.KocAiCommunity.Application.Learning;
using Beep.KocAiCommunity.Application.Security;
using Beep.KocAiCommunity.Contracts.Learning;
using Beep.KocAiCommunity.Domain.Learning;

namespace Beep.KocAiCommunity.Platform.Endpoints;

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
            var visible = (await competitions.BrowseVisibleAsync(me.UserId ?? string.Empty, ct: ct))
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

        // ---- Quizzes ------------------------------------------------------------------------------

        // Readable without an account, like the catalogue it belongs to: seeing that a track ends in a
        // quiz, and what passing takes, is part of deciding whether to start it. The returned shape has
        // no notion of a correct answer, so there is nothing here to withhold from a visitor.
        group.MapGet("/tracks/{id:guid}/quiz", async (
            Guid id, IKocCurrentUser me, IQuizService quizzes, CancellationToken ct) =>
        {
            var quiz = await quizzes.GetForLearnerAsync(id, me.UserId ?? string.Empty, ct);
            return quiz is null ? Results.NotFound() : Results.Ok(quiz);
        })
        .WithName("GetTrackQuiz")
        .AllowAnonymous();

        group.MapPost("/tracks/{id:guid}/quiz/attempts", async (
            Guid id, SubmitQuizRequest request, IKocCurrentUser me, IQuizService quizzes, CancellationToken ct) =>
        {
            try
            {
                return Results.Ok(await quizzes.SubmitAsync(id, me.UserId!, request, ct));
            }
            catch (QuizException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("SubmitQuizAttempt")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        group.MapGet("/tracks/{id:guid}/quiz/attempts", async (
            Guid id, IKocCurrentUser me, IQuizService quizzes, CancellationToken ct) =>
            Results.Ok(await quizzes.GetMyAttemptsAsync(id, me.UserId!, ct)))
        .WithName("MyQuizAttempts")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        // A certificate exists only for a track this person has actually finished. Everything printed on
        // it is read here rather than passed in, so nothing on it can be arranged by editing a URL — a
        // certificate assembled from query parameters is a template, not a record.
        group.MapGet("/tracks/{id:guid}/certificate", async (
            Guid id, IKocCurrentUser me, ILearningService learning, IQuizService quizzes,
            Beep.KocAiCommunity.Application.Engagement.IEngagementService engagement, CancellationToken ct) =>
        {
            var completion = await learning.GetCompletionAsync(id, me.UserId!, ct);
            if (completion is null)
            {
                return Results.NotFound();
            }

            var track = await learning.GetAsync(id, ct);
            if (track is null)
            {
                return Results.NotFound();
            }

            var lessons = await learning.GetLessonsAsync(id, ct);
            var attempts = await quizzes.GetMyAttemptsAsync(id, me.UserId!, ct);
            var profile = await engagement.GetProfileAsync(me.UserId!, null, ct);

            return Results.Ok(new CertificateDto(
                track.Id, track.Title, track.Level.ToString(),
                profile.DisplayName,
                completion.CompletedUtc,
                lessons.Count,
                attempts.Count == 0 ? null : attempts.Max(a => a.ScorePercent),

                // Derived from the completion row, so the same completion always prints the same
                // reference and two different ones never collide.
                completion.Id.ToString("N")[..8].ToUpperInvariant()));
        })
        .WithName("TrackCertificate")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        // ---- Quiz administration ------------------------------------------------------------------
        // These are the only endpoints that disclose which answer is correct, and they are the only ones
        // behind PlatformAdmin. The learner routes above cannot leak it: their DTOs have no such field.

        group.MapGet("/admin/tracks/{id:guid}/quiz", async (
            Guid id, IQuizService quizzes, CancellationToken ct) =>
        {
            var quiz = await quizzes.GetForAdminAsync(id, ct);
            return quiz is null ? Results.NoContent() : Results.Ok(quiz);
        })
        .WithName("GetQuizForAdmin")
        .RequireAuthorization(KocPolicies.RequirePlatformAdmin);

        group.MapPut("/admin/tracks/{id:guid}/quiz", async (
            Guid id, UpsertQuizRequest request, IKocCurrentUser me, IQuizService quizzes, CancellationToken ct) =>
        {
            try
            {
                return Results.Ok(await quizzes.UpsertAsync(id, request, me.UserId!, ct));
            }
            catch (QuizException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("UpsertQuiz")
        .RequireAuthorization(KocPolicies.RequirePlatformAdmin);

        group.MapPut("/admin/tracks/{id:guid}/quiz/questions", async (
            Guid id, UpsertQuizQuestionRequest request, IKocCurrentUser me, IQuizService quizzes, CancellationToken ct) =>
        {
            try
            {
                return Results.Ok(await quizzes.SaveQuestionAsync(id, request, me.UserId!, ct));
            }
            catch (QuizException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("SaveQuizQuestion")
        .RequireAuthorization(KocPolicies.RequirePlatformAdmin);

        group.MapDelete("/admin/tracks/{id:guid}/quiz/questions/{questionId:guid}", async (
            Guid id, Guid questionId, IKocCurrentUser me, IQuizService quizzes, CancellationToken ct) =>
        {
            try
            {
                return Results.Ok(await quizzes.DeleteQuestionAsync(id, questionId, me.UserId!, ct));
            }
            catch (QuizException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("DeleteQuizQuestion")
        .RequireAuthorization(KocPolicies.RequirePlatformAdmin);

        return group;
    }
}
