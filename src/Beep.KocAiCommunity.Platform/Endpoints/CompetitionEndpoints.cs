using Beep.KocAiCommunity.Contracts.Localization;
using Beep.KocAiCommunity.Domain.Localization;
using Beep.KocAiCommunity.Application.Localization;
using Beep.KocAiCommunity.Platform.Security;
using Beep.KocAiCommunity.Application.Authorization;
using Beep.KocAiCommunity.Application.Competitions;
using Beep.KocAiCommunity.Application.Engagement;
using Beep.KocAiCommunity.Application.Security;
using Beep.KocAiCommunity.Contracts.Competitions;
using Beep.KocAiCommunity.Contracts.Engagement;
using Beep.KocAiCommunity.Contracts.Workflow;
using Beep.KocAiCommunity.Domain.Competitions;
using Beep.KocAiCommunity.Domain.Organization;
using Microsoft.AspNetCore.Mvc;

namespace Beep.KocAiCommunity.Platform.Endpoints;

public static class CompetitionEndpoints
{
    /// <summary>
    /// The header a client sends to make a submission retry safe.
    /// <para>
    /// The conventional name, so anything that already knows the pattern gets it right. Submissions are
    /// quota-limited: a client that resends because it never saw a response — the desktop draining a
    /// queue built while offline — would otherwise have to choose between losing the work and spending
    /// a participant's attempt twice.
    /// </para>
    /// </summary>
    public const string IdempotencyHeader = "Idempotency-Key";

    public static RouteGroupBuilder MapCompetitionEndpoints(this RouteGroupBuilder group)
    {
        // One localizer for the whole group: it reads the request culture on every call,
        // so a singleton captured here still answers each request in its own language.
        var messages = group.ServiceMessages();

group.MapPost("/competitions", async (CreateCompetitionRequest req, IKocCurrentUser me, ICompetitionService svc, IScorerRegistry scorers, IContentTranslator translator, CancellationToken ct) =>
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
                var competition = await svc.CreateAsync(me.UserId!, isPlatformAdmin, req.Title, req.Description, scope, unit, req.RevealUtc, req.QuotaPerDay, req.ScorerCode, req.TaskType, ct);

                // The author may have written it in both languages. Saved against the new id, so a
                // later rename of the English title does not orphan the Arabic.
                var key = competition.Id.ToString();
                await translator.SetAsync(TranslatedContent.Competition, key, TranslatedContent.Name, KocLanguages.Arabic, req.TitleAr, ct);
                await translator.SetAsync(TranslatedContent.Competition, key, TranslatedContent.Description, KocLanguages.Arabic, req.DescriptionAr, ct);

                return Results.Ok(ToDto(competition, stats: null, scorers));
            }
            catch (CompetitionAccessException ex)
            {
                return Results.Json(new { error = messages.For(ex) }, statusCode: StatusCodes.Status403Forbidden);
            }
            catch (CompetitionException ex)
            {
                return Results.BadRequest(new { error = messages.For(ex) });
            }
        })
        .WithName("CreateCompetition")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        group.MapGet("/competitions", async (HttpContext http, IKocCurrentUser me, ICompetitionService svc, IScorerRegistry scorers, IContentTranslator translator, CancellationToken ct) =>
        {
            // Browsing the arena is open, the way Learn and Community are: you should be able to see what
            // people are competing on before deciding to sign in. Joining, downloading the data and
            // submitting all stay behind RequireEmployee below — it is the shop window that is public,
            // not the shop. A signed-out caller sees company-wide competitions only.
            var isAdmin = me.IsInRole(KocRoles.PlatformAdmin);
            var visible = me.UserId is { Length: > 0 } userId
                ? await svc.BrowseVisibleAsync(userId, isAdmin, ct)
                : await svc.BrowsePublicAllAsync(ct);

            var stats = await svc.GetStatsAsync([.. visible.Select(c => c.Id)], ct);
            var categories = await CategoryNamesAsync(svc, ct);

            // Two lookups for the whole arena rather than one per card. An untranslated challenge keeps
            // the words its author wrote, which is the same bargain the categories make above.
            var (titles, descriptions) = await CompetitionTextAsync(translator, http.RequestLanguage(), ct);

            return Results.Ok(visible
                .Select(c => Translate(ToDto(c, stats.GetValueOrDefault(c.Id), scorers, categories, me.UserId, isAdmin), titles, descriptions))
                .ToList());
        })
        .WithName("BrowseCompetitions")
        .AllowAnonymous();

        // The arena's category filter. Enabled only — a disabled category should not even be offered.
        group.MapGet("/competitions/categories", async (
            HttpContext http, ICompetitionService svc, IContentTranslator translator, CancellationToken ct) =>
        {
            var categories = await svc.ListCategoriesAsync(includeDisabled: false, ct);

            // Two lookups for the whole list, not one per row. An untranslated category keeps its
            // English name rather than showing a blank chip.
            var language = http.RequestLanguage();
            var names = await translator.LookupAsync(TranslatedContent.CompetitionCategory, TranslatedContent.Name, language, ct);
            var descriptions = await translator.LookupAsync(TranslatedContent.CompetitionCategory, TranslatedContent.Description, language, ct);

            return Results.Ok(categories
                .Select(c => new CompetitionCategoryDto(
                    c.Code,
                    names.GetValueOrDefault(c.Code, c.Name),
                    descriptions.GetValueOrDefault(c.Code, c.Description),
                    c.Icon, c.IsEnabled, c.OrderNo, 0))
                .ToList());
        })
        .WithName("BrowseCompetitionCategories")
        .AllowAnonymous();

        // What this challenge says in another language — for the author's editor, so it shows the
        // Arabic itself rather than whatever the reader's language happens to resolve to.
        group.MapGet("/competitions/{id:guid}/translations", async (
            Guid id, ICompetitionService svc, IContentTranslator translator, CancellationToken ct) =>
        {
            var competition = await svc.GetAsync(id, ct);
            if (competition is null)
            {
                return Results.NotFound();
            }

            var key = id.ToString();
            var titles = await translator.LookupAsync(TranslatedContent.Competition, TranslatedContent.Name, KocLanguages.Arabic, ct);
            var descriptions = await translator.LookupAsync(TranslatedContent.Competition, TranslatedContent.Description, KocLanguages.Arabic, ct);

            return Results.Ok(new List<CompetitionTranslationDto>
            {
                // The base text as written, labelled "en". The ordinary competition read arrives
                // translated for whoever is reading, so an Arabic-reading host editing the console
                // would otherwise see their own Arabic echoed into the English boxes.
                new(KocLanguages.English, competition.Title, competition.Description),
                new(KocLanguages.Arabic, titles.GetValueOrDefault(key), descriptions.GetValueOrDefault(key)),
            });
        })
        .WithName("GetCompetitionTranslations")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        // Only the person who created it, or a platform admin, may put words in its mouth.
        group.MapPut("/competitions/{id:guid}/translations", async (
            Guid id, SetCompetitionTranslationRequest req, IKocCurrentUser me, ICompetitionService svc,
            IContentTranslator translator, CancellationToken ct) =>
        {
            var competition = await svc.GetAsync(id, ct);
            if (competition is null)
            {
                return Results.NotFound();
            }

            if (competition.CreatedByUserId != me.UserId && !me.IsInRole(KocRoles.PlatformAdmin))
            {
                return Results.Json(new { error = "Only the competition creator can translate it." },
                    statusCode: StatusCodes.Status403Forbidden);
            }

            var language = KocLanguages.Normalize(req.Language);
            if (language == KocLanguages.English)
            {
                return Results.BadRequest(new { error = "English is the competition itself — edit the competition, not a translation of it." });
            }

            var key = id.ToString();
            await translator.SetAsync(TranslatedContent.Competition, key, TranslatedContent.Name, language, req.Title, ct);
            await translator.SetAsync(TranslatedContent.Competition, key, TranslatedContent.Description, language, req.Description, ct);
            return Results.NoContent();
        })
        .WithName("SetCompetitionTranslation")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        group.MapGet("/competitions/{id:guid}", async (
            Guid id, HttpContext http, IKocCurrentUser me, ICompetitionService svc, IScorerRegistry scorers,
            IContentTranslator translator, CancellationToken ct) =>
        {
            // A signed-out visitor reads through the leak rule, so holding the id is not enough to open a
            // team-private competition.
            var competition = me.UserId is { Length: > 0 }
                ? await svc.GetAsync(id, ct)
                : await svc.GetPublicAsync(id, ct);

            if (competition is null)
            {
                return Results.NotFound();
            }

            var stats = await svc.GetStatsAsync([id], ct);
            var (titles, descriptions) = await CompetitionTextAsync(translator, http.RequestLanguage(), ct);

            // The caller's remaining quota rides the read the page already makes — "3 of 5 left today"
            // must never cost a second request.
            var isAdmin = me.IsInRole(KocRoles.PlatformAdmin);
            int? quotaRemaining = me.UserId is { Length: > 0 } uid
                ? await svc.GetQuotaRemainingAsync(uid, id, ct)
                : null;

            return Results.Ok(Translate(
                ToDto(competition, stats.GetValueOrDefault(id), scorers, await CategoryNamesAsync(svc, ct), me.UserId, isAdmin, quotaRemaining),
                titles, descriptions));
        })
        .WithName("GetCompetition")
        .AllowAnonymous();

        group.MapPost("/competitions/{id:guid}/answer-key", async (Guid id, IFormFile file, IKocCurrentUser me, ICompetitionService svc, CancellationToken ct) =>
        {
            try
            {
                await using var stream = file.OpenReadStream();
                await svc.SetAnswerKeyAsync(me.UserId!, id, stream, me.IsInRole(KocRoles.PlatformAdmin), ct);
                return Results.NoContent();
            }
            catch (CompetitionException ex)
            {
                return Results.BadRequest(new { error = messages.For(ex) });
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
                    labelColumn ?? "label", idColumn ?? "id", task ?? "BinaryClassification",
                    me.IsInRole(KocRoles.PlatformAdmin), ct);
                return Results.NoContent();
            }
            catch (CompetitionException ex)
            {
                return Results.BadRequest(new { error = messages.For(ex) });
            }
        })
        .WithName("SetCompetitionDatasets")
        .RequireAuthorization(KocPolicies.RequireEmployee)
        .DisableAntiforgery();

        // There is deliberately no submit-pipeline route here any more. Running a competitor's graph
        // means training a model, and the website does not train — that happens in KOC Studio on the
        // desktop, on that machine's own cores. The desktop still submits a graph, but it executes it
        // itself against the database rather than asking the server to; what arrives here is a scored
        // submission, through the predictions route below.

        group.MapPost("/competitions/{id:guid}/submissions", async (Guid id, IFormFile file, IKocCurrentUser me, ICompetitionService svc,
            [FromHeader(Name = IdempotencyHeader)] string? idempotencyKey, CancellationToken ct) =>
        {
            try
            {
                await using var stream = file.OpenReadStream();
                var submission = await svc.SubmitAsync(me.UserId!, id, stream, file.FileName, idempotencyKey, ct);
                return Results.Ok(new SubmissionResultDto(submission.Id, submission.Score, submission.Status, submission.SubmittedUtc));
            }
            catch (CompetitionException ex)
            {
                return Results.BadRequest(new { error = messages.For(ex) });
            }
        })
        .WithName("Submit")
        .RequireAuthorization(KocPolicies.RequireEmployee)
        .DisableAntiforgery();

        group.MapPost("/competitions/{id:guid}/status", async (Guid id, SetStatusRequest req, IKocCurrentUser me, ICompetitionService svc, CancellationToken ct) =>
        {
            try
            {
                await svc.SetStatusAsync(me.UserId!, id, req.Status, me.IsInRole(KocRoles.PlatformAdmin), ct);
                return Results.NoContent();
            }
            catch (CompetitionException ex)
            {
                return Results.BadRequest(new { error = messages.For(ex) });
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
                return Results.BadRequest(new { error = messages.For(ex) });
            }
        })
        .WithName("SetFeaturedCompetition")
        .RequireAuthorization(KocPolicies.RequirePlatformAdmin);

        // Set the 1st/2nd/3rd podium prizes. The host's own call now — prize text is free-form and
        // visible only inside the competition's scope; there is no budget system to protect. The admin
        // console keeps working through the same route.
        group.MapPost("/competitions/{id:guid}/prizes", async (Guid id, SetPrizesRequest req, IKocCurrentUser me, ICompetitionService svc, CancellationToken ct) =>
        {
            try
            {
                await svc.SetPrizesAsync(me.UserId!, me.IsInRole(KocRoles.PlatformAdmin), id, req.FirstPrize, req.SecondPrize, req.ThirdPrize, ct);
                return Results.NoContent();
            }
            catch (CompetitionException ex)
            {
                return Results.BadRequest(new { error = messages.For(ex) });
            }
        })
        .WithName("SetCompetitionPrizes")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        // Store the web-relative path of the competition's hero image (creator or platform admin). The
        // image file itself is written to the web app's wwwroot by the caller; here we only persist the path.
        group.MapPost("/competitions/{id:guid}/hero-image", async (Guid id, SetHeroImagePathRequest req, IKocCurrentUser me, ICompetitionService svc, CancellationToken ct) =>
        {
            try
            {
                await svc.SetHeroImagePathAsync(me.UserId!, me.IsInRole(KocRoles.PlatformAdmin), id, req.Path, ct);
                return Results.NoContent();
            }
            catch (CompetitionException ex)
            {
                return Results.BadRequest(new { error = messages.For(ex) });
            }
        })
        .WithName("SetCompetitionHeroImage")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        group.MapPost("/competitions/{id:guid}/reveal", async (Guid id, SetRevealRequest req, IKocCurrentUser me, ICompetitionService svc, CancellationToken ct) =>
        {
            try
            {
                await svc.SetRevealAsync(me.UserId!, id, req.RevealUtc, req.ConcludeAtReveal, me.IsInRole(KocRoles.PlatformAdmin), ct);
                return Results.NoContent();
            }
            catch (CompetitionException ex)
            {
                return Results.BadRequest(new { error = messages.For(ex) });
            }
        })
        .WithName("SetCompetitionReveal")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        // The Host console's one save for the words and the rules. English title and description live
        // HERE — for a long time the Arabic was editable after creation and the English was not.
        group.MapPut("/competitions/{id:guid}", async (Guid id, UpdateCompetitionRequest req, IKocCurrentUser me, ICompetitionService svc, CancellationToken ct) =>
        {
            VisibilityScope? scope = null;
            if (!string.IsNullOrWhiteSpace(req.Scope))
            {
                if (!Enum.TryParse<VisibilityScope>(req.Scope, ignoreCase: true, out var parsed))
                {
                    return Results.BadRequest(new { error = $"Unknown visibility scope '{req.Scope}'." });
                }

                scope = parsed;
            }

            try
            {
                await svc.UpdateAsync(me.UserId!, me.IsInRole(KocRoles.PlatformAdmin), id, req.Title, req.Description, req.QuotaPerDay, scope, ct);
                return Results.NoContent();
            }
            catch (CompetitionAccessException ex)
            {
                return Results.Json(new { error = messages.For(ex) }, statusCode: StatusCodes.Status403Forbidden);
            }
            catch (CompetitionException ex)
            {
                return Results.BadRequest(new { error = messages.For(ex) });
            }
        })
        .WithName("UpdateCompetition")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        // Which part of KOC the challenge belongs to — the host's own choice. Without it a hosted
        // competition is invisible to every category filter on the site.
        group.MapPut("/competitions/{id:guid}/category", async (Guid id, SetCompetitionCategoryRequest req, IKocCurrentUser me, ICompetitionService svc, CancellationToken ct) =>
        {
            try
            {
                await svc.SetCompetitionCategoryAsync(me.UserId!, id, req.Code, me.IsInRole(KocRoles.PlatformAdmin), ct);
                return Results.NoContent();
            }
            catch (CompetitionException ex)
            {
                return Results.BadRequest(new { error = messages.For(ex) });
            }
        })
        .WithName("SetOwnCompetitionCategory")
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
                return Results.BadRequest(new { error = messages.For(ex) });
            }
        })
        .WithName("DownloadCompetitionData")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        group.MapGet("/competitions/{id:guid}/leaderboard", async (
            Guid id, string? board, IKocCurrentUser me, ICompetitionService svc, CancellationToken ct) =>
        {
            var competition = me.UserId is { Length: > 0 }
                ? await svc.GetAsync(id, ct)
                : await svc.GetPublicAsync(id, ct);

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
        .AllowAnonymous();

        group.MapGet("/competitions/{id:guid}/submissions", async (Guid id, IKocCurrentUser me, ICompetitionService svc, CancellationToken ct) =>
        {
            var mine = await svc.GetMySubmissionsAsync(me.UserId!, id, ct);
            return Results.Ok(mine.Select(s => new SubmissionResultDto(s.Id, s.Score, s.Status, s.SubmittedUtc)).ToList());
        })
        .WithName("MySubmissions")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        // Anonymous landing preview: active company-wide competitions, the featured live top-3, and this
        // month's top learners — everything a signed-out visitor needs to be enticed to sign in. Read-only
        // and non-private (only Company-visible competitions), so no authorization is required.
        group.MapGet("/public/showcase", async (HttpContext http, ICompetitionService svc, IEngagementService engagement, IScorerRegistry scorers, IContentTranslator translator, CancellationToken ct) =>
        {
            var list = await svc.BrowsePublicAsync(ct);
            var stats = await svc.GetStatsAsync([.. list.Select(c => c.Id)], ct);
            // The landing page is the first thing an Arabic-speaking visitor sees, so it has to be the
            // first thing translated — this was serving the featured competition in English while the
            // rest of the page had already switched.
            var (titles, descriptions) = await CompetitionTextAsync(translator, http.RequestLanguage(), ct);
            // Category names too: the landing page filters by area, and a card that knows its code but
            // not its name shows a blank chip to exactly the visitor least able to guess what it meant.
            var categories = await CategoryNamesAsync(svc, ct);
            var dtos = list
                .Select(c => Translate(ToDto(c, stats.GetValueOrDefault(c.Id), scorers, categories), titles, descriptions))
                .ToList();

            var featured = list.FirstOrDefault(c => c.IsFeatured) ?? list.FirstOrDefault();
            IReadOnlyList<LeaderboardEntryDto> board = [];
            if (featured is not null)
            {
                var entries = await svc.GetLeaderboardNamedAsync(featured.Id, "live", ct);
                board = entries.Take(PublicBoardSize)
                    .Select(e => new LeaderboardEntryDto(e.Rank, e.UserId, e.DisplayName, e.Score))
                    .ToList();
            }

            IReadOnlyList<XpLeaderboardRowDto> learners;
            try
            {
                learners = [.. (await engagement.GetXpLeaderboardAsync(string.Empty, LeaderboardPeriod.Month, ct))
                    .Take(PublicBoardSize)];
            }
            catch { learners = []; }

            return Results.Ok(new PublicShowcaseDto(featured?.Id, dtos, board, learners));
        })
        .WithName("PublicShowcase")
        .AllowAnonymous();

        return group;
    }

    /// <summary>How many rows the anonymous showcase carries — the landing page shows a full board.</summary>
    private const int PublicBoardSize = 10;

    /// <summary>Both translated fields for every competition, in two queries rather than two per row.</summary>
    private static async Task<(IReadOnlyDictionary<string, string> Titles, IReadOnlyDictionary<string, string> Descriptions)>
        CompetitionTextAsync(IContentTranslator translator, string language, CancellationToken ct) =>
        (await translator.LookupAsync(TranslatedContent.Competition, TranslatedContent.Name, language, ct),
         await translator.LookupAsync(TranslatedContent.Competition, TranslatedContent.Description, language, ct));

    /// <summary>
    /// Swaps in the translated title and description where one exists. Falls back to what the author
    /// wrote, so a half-translated arena reads as mixed language rather than as blank cards.
    /// </summary>
    private static CompetitionDto Translate(
        CompetitionDto dto, IReadOnlyDictionary<string, string> titles, IReadOnlyDictionary<string, string> descriptions)
    {
        var key = dto.Id.ToString();
        return dto with
        {
            Title = titles.GetValueOrDefault(key, dto.Title),
            Description = descriptions.GetValueOrDefault(key, dto.Description),
        };
    }

    private static CompetitionDto ToDto(
        Competition c, CompetitionStats? stats, IScorerRegistry scorers,
        IReadOnlyDictionary<string, string>? categoryNames = null,
        string? viewerUserId = null, bool viewerIsAdmin = false, int? quotaRemaining = null)
    {
        var scorer = scorers.Resolve(c.ScorerCode);
        var metric = scorer.Code.Equals("rmse", StringComparison.OrdinalIgnoreCase) ? "RMSE"
            : scorer.Code.Equals("accuracy", StringComparison.OrdinalIgnoreCase) ? "Accuracy"
            : scorer.Code.Equals("auc", StringComparison.OrdinalIgnoreCase) ? "AUC"
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
            c.IsFeatured,
            c.FirstPrize,
            c.SecondPrize,
            c.ThirdPrize,
            c.HeroImagePath is not null,
            c.HeroImagePath,
            c.CategoryCode,
            c.CategoryCode is { Length: > 0 } code ? categoryNames?.GetValueOrDefault(code) : null,
            // The scoring identity travels with every card: it is what keeps the console's Task select
            // inside the metric's family, and it costs nothing — the scorer is already resolved above.
            scorer.Code,
            [.. scorer.SupportedTasks],
            // Computed here, server-side: the page never guesses who may manage what.
            viewerUserId is { Length: > 0 } && (c.CreatedByUserId == viewerUserId || viewerIsAdmin),
            quotaRemaining,
            c.ConcludeAtReveal);
    }

    /// <summary>Category code → display name, so the DTO carries a label the UI can show without a lookup.</summary>
    private static async Task<IReadOnlyDictionary<string, string>> CategoryNamesAsync(ICompetitionService svc, CancellationToken ct) =>
        (await svc.ListCategoriesAsync(includeDisabled: true, ct))
            .ToDictionary(c => c.Code, c => c.Name, StringComparer.OrdinalIgnoreCase);
}
