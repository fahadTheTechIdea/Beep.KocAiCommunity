using System.Globalization;
using System.Text;
using Beep.KocAiCommunity.Application.Authorization;
using Beep.KocAiCommunity.Application.Common;
using Beep.KocAiCommunity.Application.Competitions;
using Beep.KocAiCommunity.Application.Datasets;
using Beep.KocAiCommunity.Application.Engagement;
using Beep.KocAiCommunity.Application.ML;
using Beep.KocAiCommunity.Application.Notifications;
using Beep.KocAiCommunity.Application.RealTime;
using Beep.KocAiCommunity.Application.Storage;
using Beep.KocAiCommunity.Application.Workflow;
using Beep.KocAiCommunity.Contracts.Workflow;
using Beep.KocAiCommunity.Domain.Authorization;
using Beep.KocAiCommunity.Domain.Common;
using Beep.KocAiCommunity.Domain.Competitions;
using Beep.KocAiCommunity.Domain.Organization;
using Beep.KocAiCommunity.Infrastructure.Engagement;
using Beep.KocAiCommunity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Beep.KocAiCommunity.Infrastructure.Competitions;

public sealed class CompetitionService(
    KocDbContext db,
    IArtifactService artifacts,
    IVisibilityEvaluator visibility,
    IScorerRegistry scorers,
    IPipelineExecutor pipeline,
    INotificationService notifications,
    IOutboxWriter outbox,
    IEngagementService engagement,
    IDatasetService datasetsSvc) : ICompetitionService
{
    // Hard wall-clock budget for training + scoring a submitted pipeline (enforced at node boundaries).
    private const int PipelineSubmitBudgetSeconds = 120;

    public async Task<Competition> CreateAsync(
        string userId, bool isPlatformAdmin, string title, string description, VisibilityScope scope, Guid visibilityOrgUnitId,
        DateTime? revealUtc, int quotaPerDay, string scorerCode, CancellationToken ct = default)
    {
        // Authorization: a platform admin may create at any level; anyone else needs an active
        // creator grant, and may only target an audience at or narrower than their granted maximum.
        var max = await GetMaxCreateScopeAsync(userId, isPlatformAdmin, ct);
        if (max is null)
        {
            throw new CompetitionAccessException("You are not authorized to create competitions. Ask an admin for access.");
        }

        if ((int)scope > (int)max.Value)
        {
            throw new CompetitionAccessException($"You can only create competitions up to {max} scope.");
        }

        // Fail fast if the scorer code is unknown.
        _ = scorers.Resolve(scorerCode);

        var competition = new Competition
        {
            Title = title,
            Description = description,
            Status = "active",
            VisibilityScope = scope,
            VisibilityOrgUnitId = visibilityOrgUnitId,
            RevealUtc = revealUtc,
            SubmissionQuotaPerDay = quotaPerDay <= 0 ? 5 : quotaPerDay,
            ScorerCode = scorerCode,
            CreatedByUserId = userId,
            CreatedUtc = DateTime.UtcNow,
        };

        db.Set<Competition>().Add(competition);
        await db.SaveChangesAsync(ct);
        return competition;
    }

    public async Task<VisibilityScope?> GetMaxCreateScopeAsync(string userId, bool isPlatformAdmin, CancellationToken ct = default)
    {
        if (isPlatformAdmin)
        {
            return VisibilityScope.Company;
        }

        var grant = await db.Set<CompetitionCreatorGrant>().FirstOrDefaultAsync(g => g.UserId == userId, ct);
        return grant is not null && grant.IsActive(DateTime.UtcNow) ? grant.MaxScope : null;
    }

    public async Task SetAnswerKeyAsync(string userId, Guid competitionId, Stream answerKey, CancellationToken ct = default)
    {
        var competition = await db.Set<Competition>().FirstOrDefaultAsync(c => c.Id == competitionId, ct)
            ?? throw new CompetitionException("Competition not found.");

        if (competition.CreatedByUserId != userId)
        {
            throw new CompetitionException("Only the competition creator can set the answer key.");
        }

        // A concluded competition's results are final — its key is locked so standings can't be rewritten
        // after podium Barrels/badges have been awarded.
        if (competition.Status == "concluded")
        {
            throw new CompetitionException("A concluded competition's answer key is locked; its results are final.");
        }

        // Buffer once so we can validate and then store the same bytes.
        using var buffer = new MemoryStream();
        await answerKey.CopyToAsync(buffer, ct);
        buffer.Position = 0;
        await ValidateAnswerKeyAsync(competition, buffer, ct);

        buffer.Position = 0;
        var artifact = await artifacts.SaveAsync(
            buffer, $"competitions/{competitionId}/answer-key.csv", "text/csv", KocDataClassification.Restricted, ct);

        competition.AnswerKeyArtifactId = artifact.Id;
        await db.SaveChangesAsync(ct);

        // Every existing score was computed against the OLD key and is now stale. Rescore all submissions
        // against the new key and rebuild the leaderboard, so the board never mixes results from two keys.
        await RescoreAllAsync(competition, ct);
    }

    // Rescores every submission for a competition against its current answer key and rebuilds the
    // leaderboard (best entry per user + ranks). Used when the key changes after submissions exist.
    private async Task RescoreAllAsync(Competition competition, CancellationToken ct)
    {
        if (competition.AnswerKeyArtifactId is null)
        {
            return;
        }

        var submissions = await db.Set<Submission>()
            .Where(s => s.CompetitionId == competition.Id)
            .ToListAsync(ct);
        if (submissions.Count == 0)
        {
            return;
        }

        var scorer = scorers.Resolve(competition.ScorerCode);

        // Buffer the key once; each submission scores against a fresh view of the same bytes.
        byte[] keyBytes;
        await using (var keyStream = await artifacts.OpenReadAsync(competition.AnswerKeyArtifactId.Value, ct))
        using (var ms = new MemoryStream())
        {
            await keyStream.CopyToAsync(ms, ct);
            keyBytes = ms.ToArray();
        }

        foreach (var submission in submissions)
        {
            var predBytes = await ReadArtifactBytesAsync(submission.PredictionArtifactId, ct);
            var (publicScore, privateScore) = await ScorePublicPrivateAsync(competition, predBytes, keyBytes, ct);
            submission.Score = publicScore;
            submission.PrivateScore = privateScore;
        }

        await db.SaveChangesAsync(ct);

        // Rebuild each user's best entry from the rescored submissions (best PUBLIC score, tie-break:
        // earliest submission), carrying that submission's private score for the final board.
        var bestPerUser = submissions
            .Where(s => s.Score.HasValue)
            .GroupBy(s => s.SubmitterUserId, StringComparer.Ordinal)
            .Select(g => (scorer.HigherIsBetter ? g.OrderByDescending(s => s.Score!.Value) : g.OrderBy(s => s.Score!.Value))
                .ThenBy(s => s.SubmittedUtc).First());

        var entries = (await db.Set<LeaderboardEntry>().Where(e => e.CompetitionId == competition.Id).ToListAsync(ct))
            .ToDictionary(e => e.SubmitterUserId, StringComparer.Ordinal);

        foreach (var best in bestPerUser)
        {
            if (entries.TryGetValue(best.SubmitterUserId, out var entry))
            {
                entry.Score = best.Score!.Value;
                entry.PrivateScore = best.PrivateScore ?? 0;
                entry.BestSubmissionId = best.Id;
            }
            else
            {
                db.Set<LeaderboardEntry>().Add(new LeaderboardEntry
                {
                    CompetitionId = competition.Id,
                    SubmitterUserId = best.SubmitterUserId,
                    BestSubmissionId = best.Id,
                    Score = best.Score!.Value,
                    PrivateScore = best.PrivateScore ?? 0,
                    CreatedUtc = DateTime.UtcNow,
                });
            }
        }

        await db.SaveChangesAsync(ct);
        await RecomputeRanksAsync(competition.Id, scorer.HigherIsBetter, ct);
    }

    // An invalid answer key silently zeroes or corrupts every submission's score, so it is rejected at
    // upload: it must cover every evaluation id exactly once, and (for rmse) hold finite numbers.
    private async Task ValidateAnswerKeyAsync(Competition competition, Stream keyStream, CancellationToken ct)
    {
        var keyRows = await CompetitionCsv.ReadAsync(keyStream, competition.IdColumn, ct);
        if (keyRows.Count == 0)
        {
            throw new CompetitionException("The answer key is empty (expected a header plus one row per evaluation id).");
        }

        var duplicates = keyRows.GroupBy(r => r.Id, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1).Select(g => g.Key).Take(5).ToList();
        if (duplicates.Count > 0)
        {
            throw new CompetitionException($"The answer key has duplicate ids: {string.Join(", ", duplicates)}.");
        }

        // When an evaluation set exists (a Studio-pipeline competition), the key must cover it exactly.
        // A direct-upload-only competition may have just an answer key, so this check is conditional.
        if (competition.EvaluationArtifactId is { } evalArtifactId)
        {
            var keyIds = new HashSet<string>(keyRows.Select(r => r.Id), StringComparer.OrdinalIgnoreCase);
            await using var evalStream = await artifacts.OpenReadAsync(evalArtifactId, ct);
            var evalIds = new HashSet<string>(await ReadColumnAsync(evalStream, competition.IdColumn, ct), StringComparer.OrdinalIgnoreCase);

            var missing = evalIds.Except(keyIds).Take(5).ToList();
            if (missing.Count > 0)
            {
                throw new CompetitionException($"The answer key is missing evaluation ids: {string.Join(", ", missing)}.");
            }

            var extra = keyIds.Except(evalIds).Take(5).ToList();
            if (extra.Count > 0)
            {
                throw new CompetitionException($"The answer key has ids not in the evaluation set: {string.Join(", ", extra)}.");
            }
        }

        if (string.Equals(competition.ScorerCode, "rmse", StringComparison.OrdinalIgnoreCase))
        {
            var bad = keyRows.Where(r => !(double.TryParse(r.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) && double.IsFinite(v)))
                .Select(r => r.Id).Take(5).ToList();
            if (bad.Count > 0)
            {
                throw new CompetitionException($"The answer key has non-numeric values for a regression competition (ids: {string.Join(", ", bad)}).");
            }
        }
    }

    // Reads one named column from a CSV stream using the shared RFC-4180 codec.
    private static async Task<List<string>> ReadColumnAsync(Stream stream, string columnName, CancellationToken ct)
    {
        using var reader = new StreamReader(stream, leaveOpen: true);
        var text = await reader.ReadToEndAsync(ct);
        var values = new List<string>();
        string[]? header = null;
        var index = 0;
        foreach (var record in KocCsv.ParseRecords(text))
        {
            if (header is null)
            {
                header = record;
                index = Array.FindIndex(header, c => c.Trim().Equals(columnName, StringComparison.OrdinalIgnoreCase));
                if (index < 0)
                {
                    index = 0;
                }

                continue;
            }

            values.Add(index < record.Length ? record[index].Trim() : string.Empty);
        }

        return values;
    }

    public async Task SetDatasetsAsync(
        string userId, Guid competitionId, Stream trainingData, Stream evaluationData,
        string labelColumn, string idColumn, string taskType, CancellationToken ct = default)
    {
        var competition = await db.Set<Competition>().FirstOrDefaultAsync(c => c.Id == competitionId, ct)
            ?? throw new CompetitionException("Competition not found.");

        if (competition.CreatedByUserId != userId)
        {
            throw new CompetitionException("Only the competition creator can set the datasets.");
        }

        var training = await artifacts.SaveAsync(
            trainingData, $"competitions/{competitionId}/training.csv", "text/csv", KocDataClassification.Internal, ct);
        var evaluation = await artifacts.SaveAsync(
            evaluationData, $"competitions/{competitionId}/evaluation.csv", "text/csv", KocDataClassification.Internal, ct);

        var resolvedTask = string.IsNullOrWhiteSpace(taskType) ? "BinaryClassification" : taskType.Trim();

        // The scorer and the task must be compatible — e.g. a Regression competition cannot be scored
        // with 'accuracy', nor a classification competition with 'rmse'. Otherwise scores are meaningless.
        var scorer = scorers.Resolve(competition.ScorerCode);
        if (!scorer.SupportedTasks.Any(t => t.Equals(resolvedTask, StringComparison.OrdinalIgnoreCase)))
        {
            throw new CompetitionException(
                $"Scorer '{competition.ScorerCode}' does not support task '{resolvedTask}'. "
                + $"It supports: {string.Join(", ", scorer.SupportedTasks)}.");
        }

        competition.TrainingDatasetArtifactId = training.Id;
        competition.EvaluationArtifactId = evaluation.Id;
        competition.LabelColumn = string.IsNullOrWhiteSpace(labelColumn) ? "label" : labelColumn.Trim();
        competition.IdColumn = string.IsNullOrWhiteSpace(idColumn) ? "id" : idColumn.Trim();
        competition.TaskType = resolvedTask;
        await db.SaveChangesAsync(ct);
    }

    private static readonly string[] LifecycleStatuses = ["draft", "active", "concluded"];

    public async Task SetStatusAsync(string userId, Guid competitionId, string status, CancellationToken ct = default)
    {
        var normalized = (status ?? string.Empty).Trim().ToLowerInvariant();
        if (!LifecycleStatuses.Contains(normalized))
        {
            throw new CompetitionException($"Unknown status '{status}'. Use draft, active, or concluded.");
        }

        var competition = await RequireCreatorAsync(userId, competitionId, ct);
        var wasConcluded = competition.Status == "concluded";
        competition.Status = normalized;
        await db.SaveChangesAsync(ct);

        // Tell every participant when a competition wraps up.
        if (normalized == "concluded" && !wasConcluded)
        {
            var participants = await db.Set<Submission>().AsNoTracking()
                .Where(s => s.CompetitionId == competitionId)
                .Select(s => s.SubmitterUserId).Distinct().ToListAsync(ct);
            await notifications.NotifyManyAsync(participants, "competition-concluded",
                $"Competition concluded: {competition.Title}",
                competition.RevealUtc is { } reveal
                    ? $"Submissions are closed. Final standings reveal {reveal.ToLocalTime():g}."
                    : "Submissions are closed. Check the leaderboard for final standings.",
                $"/compete/{competition.Id}", ct);

            await AwardPodiumAsync(competition, ct);
        }
    }

    /// <summary>At conclusion, the top three earn podium Barrels; first place earns the winner badge.</summary>
    private async Task AwardPodiumAsync(Competition competition, CancellationToken ct)
    {
        var podium = await db.Set<LeaderboardEntry>().AsNoTracking()
            .Where(e => e.CompetitionId == competition.Id && e.Rank <= 3)
            .OrderBy(e => e.Rank)
            .ToListAsync(ct);

        foreach (var entry in podium)
        {
            // 300 bbl + "On the Podium" badge for the top three; refId=competition keeps it idempotent.
            await AwardSafelyAsync(entry.SubmitterUserId, XpSources.CompetitionTop3, "competition", competition.Id, ct);

            // A 0-bbl marker that unlocks the "Gusher" winner badge for first place.
            if (entry.Rank == 1)
            {
                await AwardSafelyAsync(entry.SubmitterUserId, XpSources.CompetitionWin, "competition", competition.Id, ct);
            }
        }
    }

    // Engagement is a side effect: never let an award failure fail a competition action.
    private async Task AwardSafelyAsync(string userId, string source, string refType, Guid refId, CancellationToken ct)
    {
        try
        {
            await engagement.AwardXpAsync(userId, source, refType, refId, ct);
        }
        catch (Exception)
        {
            // Swallow — the competition action already committed.
        }
    }

    public async Task SetRevealAsync(string userId, Guid competitionId, DateTime? revealUtc, CancellationToken ct = default)
    {
        var competition = await RequireCreatorAsync(userId, competitionId, ct);
        competition.RevealUtc = revealUtc;
        await db.SaveChangesAsync(ct);
    }

    private async Task<Competition> RequireCreatorAsync(string userId, Guid competitionId, CancellationToken ct)
    {
        var competition = await db.Set<Competition>().FirstOrDefaultAsync(c => c.Id == competitionId, ct)
            ?? throw new CompetitionException("Competition not found.");

        if (competition.CreatedByUserId != userId)
        {
            throw new CompetitionException("Only the competition creator can change its lifecycle.");
        }

        return competition;
    }

    public async Task<IReadOnlyList<Competition>> BrowseVisibleAsync(string userId, CancellationToken ct = default)
    {
        var open = await db.Set<Competition>().AsNoTracking()
            .Where(c => c.Status != "draft")
            .OrderByDescending(c => c.CreatedUtc)
            .ToListAsync(ct);

        var visible = new List<Competition>(open.Count);
        foreach (var competition in open)
        {
            if (await visibility.CanSeeAsync(userId, competition.VisibilityScope, competition.VisibilityOrgUnitId, ct))
            {
                visible.Add(competition);
            }
        }

        return visible;
    }

    public Task<Competition?> GetAsync(Guid competitionId, CancellationToken ct = default) =>
        db.Set<Competition>().AsNoTracking().FirstOrDefaultAsync(c => c.Id == competitionId, ct);

    public async Task SetFeaturedAsync(Guid competitionId, CancellationToken ct = default)
    {
        var competition = await db.Set<Competition>().FirstOrDefaultAsync(c => c.Id == competitionId, ct)
            ?? throw new CompetitionException("Competition not found.");

        // Exactly one hero at a time: clear the flag on whatever was featured, set it on this one.
        var previouslyFeatured = await db.Set<Competition>().Where(c => c.IsFeatured && c.Id != competitionId).ToListAsync(ct);
        foreach (var other in previouslyFeatured)
        {
            other.IsFeatured = false;
        }

        competition.IsFeatured = true;
        await db.SaveChangesAsync(ct);
    }

    public Task<Competition?> GetFeaturedAsync(CancellationToken ct = default) =>
        db.Set<Competition>().AsNoTracking().FirstOrDefaultAsync(c => c.IsFeatured, ct);

    public async Task<Submission> SubmitAsync(string userId, Guid competitionId, Stream predictions, string fileName, CancellationToken ct = default)
    {
        var competition = await db.Set<Competition>().FirstOrDefaultAsync(c => c.Id == competitionId, ct)
            ?? throw new CompetitionException("Competition not found.");

        await EnsureSubmittableAsync(competition, userId, ct);
        return await ScoreAndRecordAsync(competition, userId, predictions, fileName, notes: null, ct);
    }

    public async Task<Submission> SubmitPipelineAsync(string userId, Guid competitionId, WorkflowDefinition definition, CancellationToken ct = default)
    {
        var competition = await db.Set<Competition>().FirstOrDefaultAsync(c => c.Id == competitionId, ct)
            ?? throw new CompetitionException("Competition not found.");

        await EnsureSubmittableAsync(competition, userId, ct);

        if (competition.TrainingDatasetArtifactId is null || competition.EvaluationArtifactId is null)
        {
            throw new CompetitionException("This competition has no training/evaluation data for pipeline submissions.");
        }

        if (!Enum.TryParse<MlTaskType>(competition.TaskType, ignoreCase: true, out var task))
        {
            task = MlTaskType.BinaryClassification;
        }

        // Run the participant's pipeline on the competition's authoritative data — participants
        // never supply the primary inputs, so the score reflects only their modelling choices. Any
        // join/union nodes may still bring in the participant's OWN visible datasets (feature engineering),
        // resolved and passed as secondaries; label/id/task stay the competition's (authoritative override).
        var secondary = await ResolveSecondaryDatasetsAsync(userId, definition, ct);

        // Bound a runaway/expensive pipeline so it can't tie up the request indefinitely; the executor
        // checks the token at each node boundary and fails fast.
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(PipelineSubmitBudgetSeconds));
        string predictionCsv;
        try
        {
            await using var trainStream = await artifacts.OpenReadAsync(competition.TrainingDatasetArtifactId.Value, ct);
            await using var evalStream = await artifacts.OpenReadAsync(competition.EvaluationArtifactId.Value, ct);
            predictionCsv = await pipeline.PredictAsync(
                definition, competition.LabelColumn, competition.IdColumn, task, trainStream, evalStream, secondary, timeout.Token);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            throw new CompetitionException($"The pipeline took longer than {PipelineSubmitBudgetSeconds}s to train and score. Simplify it and try again.");
        }

        foreach (var s in secondary.Values)
        {
            await s.DisposeAsync();
        }

        using var predictions = new MemoryStream(Encoding.UTF8.GetBytes(predictionCsv));
        return await ScoreAndRecordAsync(competition, userId, predictions, "pipeline-predictions.csv", notes: "from Studio pipeline", ct);
    }

    // Resolve the join/union nodes' secondary datasets to the participant's OWN visible, file-bearing
    // datasets so feature-engineering joins work on submit (mirrors the free-run path). Datasets the
    // participant can't see are skipped — the executor then fails that node loudly with a clear message.
    private async Task<IReadOnlyDictionary<Guid, Stream>> ResolveSecondaryDatasetsAsync(
        string userId, WorkflowDefinition definition, CancellationToken ct)
    {
        var map = new Dictionary<Guid, Stream>();
        foreach (var id in WorkflowDatasetScanner.ReferencedDatasetIds(definition))
        {
            var dataset = await datasetsSvc.GetVisibleAsync(userId, id, ct);
            if (dataset?.FileArtifactId is not { } fileId)
            {
                continue;
            }

            var ms = new MemoryStream();
            await using (var s = await artifacts.OpenReadAsync(fileId, ct))
            {
                await s.CopyToAsync(ms, ct);
            }

            ms.Position = 0;
            map[id] = ms;
        }

        return map;
    }

    private async Task EnsureSubmittableAsync(Competition competition, string userId, CancellationToken ct)
    {
        if (!await visibility.CanSeeAsync(userId, competition.VisibilityScope, competition.VisibilityOrgUnitId, ct))
        {
            throw new CompetitionException("This competition is not visible to you.");
        }

        if (competition.Status != "active" || competition.AnswerKeyArtifactId is null)
        {
            throw new CompetitionException("This competition is not open for submissions.");
        }

        var sinceUtc = DateTime.UtcNow.Date;
        var todayCount = await db.Set<Submission>()
            .CountAsync(s => s.CompetitionId == competition.Id && s.SubmitterUserId == userId && s.SubmittedUtc >= sinceUtc, ct);
        if (todayCount >= competition.SubmissionQuotaPerDay)
        {
            throw new CompetitionException($"Daily submission quota ({competition.SubmissionQuotaPerDay}) reached.");
        }
    }

    private async Task<Submission> ScoreAndRecordAsync(Competition competition, string userId, Stream predictions, string fileName, string? notes, CancellationToken ct)
    {
        var predictionArtifact = await artifacts.SaveAsync(
            predictions, $"competitions/{competition.Id}/submissions/{userId}/{fileName}", "text/csv", KocDataClassification.Internal, ct);

        var predBytes = await ReadArtifactBytesAsync(predictionArtifact.Id, ct);
        var keyBytes = await ReadArtifactBytesAsync(competition.AnswerKeyArtifactId!.Value, ct);
        await EnsureFullCoverageAsync(competition, predBytes, keyBytes, ct);
        var (score, privateScore) = await ScorePublicPrivateAsync(competition, predBytes, keyBytes, ct);

        var submission = new Submission
        {
            CompetitionId = competition.Id,
            SubmitterUserId = userId,
            PredictionArtifactId = predictionArtifact.Id,
            SubmittedUtc = DateTime.UtcNow,
            Status = "scored",
            Score = score,               // public — shown live
            PrivateScore = privateScore, // hidden holdout — the final standings
            Notes = notes,
            CreatedUtc = DateTime.UtcNow,
        };
        db.Set<Submission>().Add(submission);
        await db.SaveChangesAsync(ct);

        await UpdateLeaderboardAsync(competition, userId, submission, score, privateScore, ct);

        await notifications.NotifyAsync(userId, "submission-scored",
            $"Submission scored: {score:0.###}",
            $"Your submission to \"{competition.Title}\" scored {score:0.###}.",
            $"/compete/{competition.Id}", ct);

        // Barrels for a scored submission (idempotent per submission; a first-ever submission earns a bonus + badge).
        await AwardSafelyAsync(userId, XpSources.SubmissionScored, "submission", submission.Id, ct);
        return submission;
    }

    // Every answer-key id must have a prediction, or the score would silently reflect only a subset. The
    // pipeline path always emits one prediction per eval id; this guards the direct-upload path, where a user
    // could upload a partial file.
    private static async Task EnsureFullCoverageAsync(Competition competition, byte[] predBytes, byte[] keyBytes, CancellationToken ct)
    {
        var predIds = (await CompetitionCsv.ReadAsync(new MemoryStream(predBytes), competition.IdColumn, ct))
            .Select(r => r.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var keyRows = await CompetitionCsv.ReadAsync(new MemoryStream(keyBytes), competition.IdColumn, ct);
        var missing = keyRows.Count(r => !predIds.Contains(r.Id));
        if (missing > 0)
        {
            throw new CompetitionException(
                $"Your submission is missing predictions for {missing} of {keyRows.Count} rows. Provide a prediction for every id.");
        }
    }

    // Scores a prediction against the public (live leaderboard) and private (concealed-final) holdout
    // subsets of the answer key. The split is deterministic (a stable hash of each id, halved), so a
    // participant can't reverse-engineer the private set from the live board, and the final standings on
    // the hidden half only settle at the reveal — the Kaggle-style public/private leaderboard.
    private async Task<(double Public, double Private)> ScorePublicPrivateAsync(Competition competition, byte[] predBytes, byte[] keyBytes, CancellationToken ct)
    {
        var scorer = scorers.Resolve(competition.ScorerCode);
        var keyRows = await CompetitionCsv.ReadAsync(new MemoryStream(keyBytes), competition.IdColumn, ct);
        var (publicKey, privateKey) = PartitionAnswerKey(competition.IdColumn, keyRows);
        var publicScore = await scorer.ScoreAsync(new MemoryStream(predBytes), new MemoryStream(publicKey), competition.IdColumn, ct);
        var privateScore = await scorer.ScoreAsync(new MemoryStream(predBytes), new MemoryStream(privateKey), competition.IdColumn, ct);
        return (publicScore, privateScore);
    }

    private async Task<byte[]> ReadArtifactBytesAsync(Guid artifactId, CancellationToken ct)
    {
        await using var stream = await artifacts.OpenReadAsync(artifactId, ct);
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct);
        return ms.ToArray();
    }

    // Deterministic ~50/50 holdout: order the key rows by a stable hash of the id and split at the
    // midpoint. Stable for a fixed key; both subsets are non-empty whenever the key has ≥2 rows. A key too
    // small to hold out scores against the whole key on both boards.
    private static (byte[] Public, byte[] Private) PartitionAnswerKey(string idColumn, IReadOnlyList<(string Id, string Value)> keyRows)
    {
        if (keyRows.Count < 2)
        {
            var all = WriteKeyCsv(idColumn, keyRows);
            return (all, all);
        }

        var ordered = keyRows.OrderBy(r => StableHash(r.Id)).ThenBy(r => r.Id, StringComparer.Ordinal).ToList();
        var mid = ordered.Count / 2;
        return (WriteKeyCsv(idColumn, ordered.Take(mid)), WriteKeyCsv(idColumn, ordered.Skip(mid)));
    }

    private static byte[] WriteKeyCsv(string idColumn, IEnumerable<(string Id, string Value)> rows)
    {
        var sb = new StringBuilder();
        sb.Append(KocCsv.WriteRow([idColumn, "value"])).Append('\n');
        foreach (var (id, value) in rows)
        {
            sb.Append(KocCsv.WriteRow([id, value])).Append('\n');
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    // FNV-1a — a small, process-stable string hash (unlike string.GetHashCode, which is randomised).
    private static uint StableHash(string value)
    {
        unchecked
        {
            var hash = 2166136261u;
            foreach (var c in value)
            {
                hash = (hash ^ c) * 16777619u;
            }

            return hash;
        }
    }

    public async Task<IReadOnlyList<LeaderboardEntry>> GetLeaderboardAsync(Guid competitionId, CancellationToken ct = default) =>
        await db.Set<LeaderboardEntry>().AsNoTracking()
            .Where(e => e.CompetitionId == competitionId)
            .OrderBy(e => e.Rank)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<NamedLeaderboardEntry>> GetLeaderboardNamedAsync(Guid competitionId, string board, CancellationToken ct = default)
    {
        var entries = await db.Set<LeaderboardEntry>().AsNoTracking()
            .Where(e => e.CompetitionId == competitionId)
            .ToListAsync(ct);
        var ids = entries.Select(e => e.SubmitterUserId).Distinct().ToList();
        var names = await db.Set<Domain.Engagement.UserProfile>().AsNoTracking()
            .Where(p => ids.Contains(p.UserId))
            .ToDictionaryAsync(p => p.UserId, p => p.DisplayName, ct);
        string Name(string u) => names.GetValueOrDefault(u, u);

        if (string.Equals(board, "final", StringComparison.OrdinalIgnoreCase))
        {
            // Concealed final board: rank by the hidden private-holdout score (the caller has already
            // verified the reveal time has passed).
            var scorerCode = await db.Set<Competition>().AsNoTracking()
                .Where(c => c.Id == competitionId).Select(c => c.ScorerCode).FirstOrDefaultAsync(ct) ?? "accuracy";
            var higherIsBetter = scorers.Resolve(scorerCode).HigherIsBetter;
            var ordered = (higherIsBetter ? entries.OrderByDescending(e => e.PrivateScore) : entries.OrderBy(e => e.PrivateScore))
                .ThenBy(e => e.CreatedUtc)
                .ThenBy(e => e.SubmitterUserId, StringComparer.Ordinal)
                .ToList();
            return ordered.Select((e, i) => new NamedLeaderboardEntry(i + 1, e.SubmitterUserId, Name(e.SubmitterUserId), e.PrivateScore)).ToList();
        }

        // Live board: the stored public score and rank.
        return entries.OrderBy(e => e.Rank)
            .Select(e => new NamedLeaderboardEntry(e.Rank, e.SubmitterUserId, Name(e.SubmitterUserId), e.Score))
            .ToList();
    }

    public async Task<IReadOnlyDictionary<Guid, CompetitionStats>> GetStatsAsync(IReadOnlyCollection<Guid> competitionIds, CancellationToken ct = default)
    {
        if (competitionIds.Count == 0)
        {
            return new Dictionary<Guid, CompetitionStats>();
        }

        // One grouped pass over submissions for participant + submission counts.
        var counts = await db.Set<Submission>().AsNoTracking()
            .Where(s => competitionIds.Contains(s.CompetitionId))
            .GroupBy(s => s.CompetitionId)
            .Select(g => new { g.Key, Participants = g.Select(s => s.SubmitterUserId).Distinct().Count(), Submissions = g.Count() })
            .ToDictionaryAsync(x => x.Key, ct);

        // Host display names via the profile table (falls back to the raw user id).
        var hosts = await db.Set<Competition>().AsNoTracking()
            .Where(c => competitionIds.Contains(c.Id))
            .Select(c => new { c.Id, HostId = c.CreatedByUserId ?? "" })
            .ToListAsync(ct);
        var hostIds = hosts.Select(h => h.HostId).Distinct().ToList();
        var names = await db.Set<Domain.Engagement.UserProfile>().AsNoTracking()
            .Where(p => hostIds.Contains(p.UserId))
            .ToDictionaryAsync(p => p.UserId, p => p.DisplayName, ct);

        return hosts.ToDictionary(
            h => h.Id,
            h => new CompetitionStats(
                counts.TryGetValue(h.Id, out var c) ? c.Participants : 0,
                counts.TryGetValue(h.Id, out var c2) ? c2.Submissions : 0,
                names.GetValueOrDefault(h.HostId, h.HostId)));
    }

    public async Task<IReadOnlyList<Submission>> GetMySubmissionsAsync(string userId, Guid competitionId, CancellationToken ct = default) =>
        await db.Set<Submission>().AsNoTracking()
            .Where(s => s.CompetitionId == competitionId && s.SubmitterUserId == userId)
            .OrderByDescending(s => s.SubmittedUtc)
            .ToListAsync(ct);

    public async Task<Stream?> OpenDatasetAsync(string userId, Guid competitionId, string which, CancellationToken ct = default)
    {
        var competition = await db.Set<Competition>().AsNoTracking().FirstOrDefaultAsync(c => c.Id == competitionId, ct)
            ?? throw new CompetitionException("Competition not found.");

        if (!await visibility.CanSeeAsync(userId, competition.VisibilityScope, competition.VisibilityOrgUnitId, ct))
        {
            throw new CompetitionException("This competition is not visible to you.");
        }

        // Only participant-visible datasets are downloadable — never the answer key.
        var artifactId = which.Equals("evaluation", StringComparison.OrdinalIgnoreCase)
            ? competition.EvaluationArtifactId
            : which.Equals("training", StringComparison.OrdinalIgnoreCase)
                ? competition.TrainingDatasetArtifactId
                : throw new CompetitionException("Unknown dataset. Use 'training' or 'evaluation'.");

        return artifactId is null ? null : await artifacts.OpenReadAsync(artifactId.Value, ct);
    }

    private async Task UpdateLeaderboardAsync(Competition competition, string userId, Submission submission, double score, double privateScore, CancellationToken ct)
    {
        var higherIsBetter = scorers.Resolve(competition.ScorerCode).HigherIsBetter;

        var entry = await db.Set<LeaderboardEntry>()
            .FirstOrDefaultAsync(e => e.CompetitionId == competition.Id && e.SubmitterUserId == userId, ct);

        // The live board tracks the best PUBLIC score; the private score of that same submission is carried
        // so the concealed final board can rank on the hidden holdout at reveal time.
        if (entry is null)
        {
            db.Set<LeaderboardEntry>().Add(new LeaderboardEntry
            {
                CompetitionId = competition.Id,
                SubmitterUserId = userId,
                BestSubmissionId = submission.Id,
                Score = score,
                PrivateScore = privateScore,
                CreatedUtc = DateTime.UtcNow,
            });
        }
        else if ((higherIsBetter && score > entry.Score) || (!higherIsBetter && score < entry.Score))
        {
            entry.Score = score;
            entry.PrivateScore = privateScore;
            entry.BestSubmissionId = submission.Id;
        }

        await db.SaveChangesAsync(ct);
        await RecomputeRanksAsync(competition.Id, higherIsBetter, ct);
    }

    // Recompute ranks (1 = best) for a competition's leaderboard. Ties break deterministically by earliest
    // entry, then user id, so equal scores always rank the same way regardless of database fetch order.
    // The rank rewrite touches every entry, so two submissions to the same competition can race; the
    // RowVersion concurrency token turns a lost update into a DbUpdateConcurrencyException, which we
    // resolve by reloading the current scores and recomputing (bounded retry).
    private async Task RecomputeRanksAsync(Guid competitionId, bool higherIsBetter, CancellationToken ct)
    {
        const int maxAttempts = 4;
        for (var attempt = 1; ; attempt++)
        {
            var entries = await db.Set<LeaderboardEntry>().Where(e => e.CompetitionId == competitionId).ToListAsync(ct);
            var ordered = (higherIsBetter ? entries.OrderByDescending(e => e.Score) : entries.OrderBy(e => e.Score))
                .ThenBy(e => e.CreatedUtc)
                .ThenBy(e => e.SubmitterUserId, StringComparer.Ordinal)
                .ToList();

            for (var i = 0; i < ordered.Count; i++)
            {
                ordered[i].Rank = i + 1;
            }

            try
            {
                await db.SaveChangesAsync(ct);
                break;
            }
            catch (DbUpdateConcurrencyException) when (attempt < maxAttempts)
            {
                // A concurrent submission renumbered the board first; drop our stale reads and recompute.
                foreach (var e in entries)
                {
                    await db.Entry(e).ReloadAsync(ct);
                }
            }
        }

        // Relayed to the competition's SignalR group so open leaderboards refresh live.
        await outbox.EnqueueAsync(new LeaderboardUpdatedEvent(competitionId), ct);
        await db.SaveChangesAsync(ct);
    }
}
