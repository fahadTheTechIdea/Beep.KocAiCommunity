using Beep.KocAiCommunity.Application.Authorization;
using Beep.KocAiCommunity.Application.Competitions;
using Beep.KocAiCommunity.Application.Security;
using Beep.KocAiCommunity.Contracts.Competitions;
using Beep.KocAiCommunity.Contracts.Workflow;
using Beep.KocAiCommunity.Domain.Competitions;
using Beep.KocAiCommunity.Domain.Organization;

namespace Beep.KocAiCommunity.Api.Endpoints;

public static class CompetitionEndpoints
{
    public static RouteGroupBuilder MapCompetitionEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/competitions", async (CreateCompetitionRequest req, IKocCurrentUser me, ICompetitionService svc, IScorerRegistry scorers, CancellationToken ct) =>
        {
            if (!Enum.TryParse<VisibilityScope>(req.Scope, ignoreCase: true, out var scope))
            {
                return Results.BadRequest(new { error = $"Unknown visibility scope '{req.Scope}'." });
            }

            var unit = scope == VisibilityScope.Company
                ? Guid.Empty
                : req.VisibilityOrgUnitId ?? me.HomeOrgUnitId ?? Guid.Empty;

            try
            {
                var isPlatformAdmin = me.IsInRole(KocRoles.PlatformAdmin);
                var competition = await svc.CreateAsync(me.UserId!, isPlatformAdmin, req.Title, req.Description, scope, unit, req.RevealUtc, req.QuotaPerDay, req.ScorerCode, ct);
                return Results.Ok(ToDto(competition, stats: null, scorers));
            }
            catch (CompetitionAccessException ex)
            {
                return Results.Json(new { error = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
            }
            catch (CompetitionException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("CreateCompetition")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        group.MapGet("/competitions", async (IKocCurrentUser me, ICompetitionService svc, IScorerRegistry scorers, CancellationToken ct) =>
        {
            var visible = await svc.BrowseVisibleAsync(me.UserId!, ct);
            var stats = await svc.GetStatsAsync([.. visible.Select(c => c.Id)], ct);
            return Results.Ok(visible.Select(c => ToDto(c, stats.GetValueOrDefault(c.Id), scorers)).ToList());
        })
        .WithName("BrowseCompetitions")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        group.MapGet("/competitions/{id:guid}", async (Guid id, ICompetitionService svc, IScorerRegistry scorers, CancellationToken ct) =>
        {
            var competition = await svc.GetAsync(id, ct);
            if (competition is null)
            {
                return Results.NotFound();
            }

            var stats = await svc.GetStatsAsync([id], ct);
            return Results.Ok(ToDto(competition, stats.GetValueOrDefault(id), scorers));
        })
        .WithName("GetCompetition")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        group.MapPost("/competitions/{id:guid}/answer-key", async (Guid id, IFormFile file, IKocCurrentUser me, ICompetitionService svc, CancellationToken ct) =>
        {
            try
            {
                await using var stream = file.OpenReadStream();
                await svc.SetAnswerKeyAsync(me.UserId!, id, stream, ct);
                return Results.NoContent();
            }
            catch (CompetitionException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("SetAnswerKey")
        .RequireAuthorization(KocPolicies.RequireEmployee)
        .DisableAntiforgery();

        group.MapPost("/competitions/{id:guid}/datasets", async (
            Guid id, IFormFile training, IFormFile evaluation,
            string? labelColumn, string? idColumn, string? task,
            IKocCurrentUser me, ICompetitionService svc, CancellationToken ct) =>
        {
            try
            {
                await using var trainStream = training.OpenReadStream();
                await using var evalStream = evaluation.OpenReadStream();
                await svc.SetDatasetsAsync(me.UserId!, id, trainStream, evalStream,
                    labelColumn ?? "label", idColumn ?? "id", task ?? "BinaryClassification", ct);
                return Results.NoContent();
            }
            catch (CompetitionException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("SetCompetitionDatasets")
        .RequireAuthorization(KocPolicies.RequireEmployee)
        .DisableAntiforgery();

        group.MapPost("/competitions/{id:guid}/submit-pipeline", async (
            Guid id, WorkflowDefinition definition, IKocCurrentUser me, ICompetitionService svc, CancellationToken ct) =>
        {
            try
            {
                var submission = await svc.SubmitPipelineAsync(me.UserId!, id, definition, ct);
                return Results.Ok(new SubmissionResultDto(submission.Id, submission.Score, submission.Status, submission.SubmittedUtc));
            }
            catch (CompetitionException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("SubmitPipeline")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        group.MapPost("/competitions/{id:guid}/submissions", async (Guid id, IFormFile file, IKocCurrentUser me, ICompetitionService svc, CancellationToken ct) =>
        {
            try
            {
                await using var stream = file.OpenReadStream();
                var submission = await svc.SubmitAsync(me.UserId!, id, stream, file.FileName, ct);
                return Results.Ok(new SubmissionResultDto(submission.Id, submission.Score, submission.Status, submission.SubmittedUtc));
            }
            catch (CompetitionException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("Submit")
        .RequireAuthorization(KocPolicies.RequireEmployee)
        .DisableAntiforgery();

        group.MapPost("/competitions/{id:guid}/status", async (Guid id, SetStatusRequest req, IKocCurrentUser me, ICompetitionService svc, CancellationToken ct) =>
        {
            try
            {
                await svc.SetStatusAsync(me.UserId!, id, req.Status, ct);
                return Results.NoContent();
            }
            catch (CompetitionException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("SetCompetitionStatus")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        // Pin one competition as the landing-page hero (platform admin only, one at a time).
        group.MapPost("/competitions/{id:guid}/feature", async (Guid id, ICompetitionService svc, CancellationToken ct) =>
        {
            try
            {
                await svc.SetFeaturedAsync(id, ct);
                return Results.NoContent();
            }
            catch (CompetitionException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("SetFeaturedCompetition")
        .RequireAuthorization(KocPolicies.RequirePlatformAdmin);

        group.MapPost("/competitions/{id:guid}/reveal", async (Guid id, SetRevealRequest req, IKocCurrentUser me, ICompetitionService svc, CancellationToken ct) =>
        {
            try
            {
                await svc.SetRevealAsync(me.UserId!, id, req.RevealUtc, ct);
                return Results.NoContent();
            }
            catch (CompetitionException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("SetCompetitionReveal")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        group.MapGet("/competitions/{id:guid}/data/{which}", async (Guid id, string which, IKocCurrentUser me, ICompetitionService svc, CancellationToken ct) =>
        {
            try
            {
                var stream = await svc.OpenDatasetAsync(me.UserId!, id, which, ct);
                return stream is null
                    ? Results.NotFound(new { error = $"No {which} dataset has been set." })
                    : Results.File(stream, "text/csv", $"{which}.csv");
            }
            catch (CompetitionException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("DownloadCompetitionData")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        group.MapGet("/competitions/{id:guid}/leaderboard", async (Guid id, string? board, ICompetitionService svc, CancellationToken ct) =>
        {
            var competition = await svc.GetAsync(id, ct);
            if (competition is null)
            {
                return Results.NotFound();
            }

            // The concealed final board (ranked on the hidden private holdout) is hidden until the reveal
            // time; the live board (the public holdout) is always visible during the competition.
            if (string.Equals(board, "final", StringComparison.OrdinalIgnoreCase)
                && competition.RevealUtc is { } reveal && reveal > DateTime.UtcNow)
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var entries = await svc.GetLeaderboardNamedAsync(id, board ?? "live", ct);
            return Results.Ok(entries.Select(e => new LeaderboardEntryDto(e.Rank, e.UserId, e.DisplayName, e.Score)).ToList());
        })
        .WithName("Leaderboard")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        group.MapGet("/competitions/{id:guid}/submissions", async (Guid id, IKocCurrentUser me, ICompetitionService svc, CancellationToken ct) =>
        {
            var mine = await svc.GetMySubmissionsAsync(me.UserId!, id, ct);
            return Results.Ok(mine.Select(s => new SubmissionResultDto(s.Id, s.Score, s.Status, s.SubmittedUtc)).ToList());
        })
        .WithName("MySubmissions")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        return group;
    }

    private static CompetitionDto ToDto(Competition c, CompetitionStats? stats, IScorerRegistry scorers)
    {
        var scorer = scorers.Resolve(c.ScorerCode);
        var metric = scorer.Code.Equals("rmse", StringComparison.OrdinalIgnoreCase) ? "RMSE"
            : scorer.Code.Equals("accuracy", StringComparison.OrdinalIgnoreCase) ? "Accuracy"
            : scorer.Code;
        return new(c.Id, c.Title, c.Description, c.Status, c.VisibilityScope.ToString(), c.RevealUtc,
            c.AnswerKeyArtifactId is not null,
            c.TrainingDatasetArtifactId is not null && c.EvaluationArtifactId is not null,
            c.LabelColumn, c.IdColumn, c.TaskType, c.RecommendedTrackId,
            stats?.ParticipantCount ?? 0,
            stats?.SubmissionCount ?? 0,
            stats?.HostName ?? c.CreatedByUserId ?? "",
            c.SubmissionQuotaPerDay,
            metric,
            scorer.HigherIsBetter,
            c.CreatedUtc,
            c.IsFeatured);
    }
}
