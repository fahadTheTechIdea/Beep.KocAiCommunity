using System.Text;
using Beep.KocAiCommunity.Application.Authorization;
using Beep.KocAiCommunity.Application.Competitions;
using Beep.KocAiCommunity.Application.Engagement;
using Beep.KocAiCommunity.Application.ML;
using Beep.KocAiCommunity.Application.Notifications;
using Beep.KocAiCommunity.Application.RealTime;
using Beep.KocAiCommunity.Application.Storage;
using Beep.KocAiCommunity.Application.Workflow;
using Beep.KocAiCommunity.Contracts.Workflow;
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
    IEngagementService engagement) : ICompetitionService
{
    public async Task<Competition> CreateAsync(
        string userId, string title, string description, VisibilityScope scope, Guid visibilityOrgUnitId,
        DateTime? revealUtc, int quotaPerDay, string scorerCode, CancellationToken ct = default)
    {
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

    public async Task SetAnswerKeyAsync(string userId, Guid competitionId, Stream answerKey, CancellationToken ct = default)
    {
        var competition = await db.Set<Competition>().FirstOrDefaultAsync(c => c.Id == competitionId, ct)
            ?? throw new CompetitionException("Competition not found.");

        if (competition.CreatedByUserId != userId)
        {
            throw new CompetitionException("Only the competition creator can set the answer key.");
        }

        var artifact = await artifacts.SaveAsync(
            answerKey, $"competitions/{competitionId}/answer-key.csv", "text/csv", KocDataClassification.Restricted, ct);

        competition.AnswerKeyArtifactId = artifact.Id;
        await db.SaveChangesAsync(ct);
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

        competition.TrainingDatasetArtifactId = training.Id;
        competition.EvaluationArtifactId = evaluation.Id;
        competition.LabelColumn = string.IsNullOrWhiteSpace(labelColumn) ? "label" : labelColumn.Trim();
        competition.IdColumn = string.IsNullOrWhiteSpace(idColumn) ? "id" : idColumn.Trim();
        competition.TaskType = string.IsNullOrWhiteSpace(taskType) ? "BinaryClassification" : taskType.Trim();
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
                "/compete", ct);

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
        // never supply the inputs, so the score reflects only their modelling choices.
        string predictionCsv;
        await using (var trainStream = await artifacts.OpenReadAsync(competition.TrainingDatasetArtifactId.Value, ct))
        await using (var evalStream = await artifacts.OpenReadAsync(competition.EvaluationArtifactId.Value, ct))
        {
            predictionCsv = await pipeline.PredictAsync(
                definition, competition.LabelColumn, competition.IdColumn, task, trainStream, evalStream, ct);
        }

        using var predictions = new MemoryStream(Encoding.UTF8.GetBytes(predictionCsv));
        return await ScoreAndRecordAsync(competition, userId, predictions, "pipeline-predictions.csv", notes: "from Studio pipeline", ct);
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

        double score;
        await using (var predStream = await artifacts.OpenReadAsync(predictionArtifact.Id, ct))
        await using (var keyStream = await artifacts.OpenReadAsync(competition.AnswerKeyArtifactId!.Value, ct))
        {
            score = await scorers.Resolve(competition.ScorerCode).ScoreAsync(predStream, keyStream, ct);
        }

        var submission = new Submission
        {
            CompetitionId = competition.Id,
            SubmitterUserId = userId,
            PredictionArtifactId = predictionArtifact.Id,
            SubmittedUtc = DateTime.UtcNow,
            Status = "scored",
            Score = score,
            Notes = notes,
            CreatedUtc = DateTime.UtcNow,
        };
        db.Set<Submission>().Add(submission);
        await db.SaveChangesAsync(ct);

        await UpdateLeaderboardAsync(competition, userId, submission, score, ct);

        await notifications.NotifyAsync(userId, "submission-scored",
            $"Submission scored: {score:0.###}",
            $"Your submission to \"{competition.Title}\" scored {score:0.###}.",
            "/compete", ct);

        // Barrels for a scored submission (idempotent per submission; a first-ever submission earns a bonus + badge).
        await AwardSafelyAsync(userId, XpSources.SubmissionScored, "submission", submission.Id, ct);
        return submission;
    }

    public async Task<IReadOnlyList<LeaderboardEntry>> GetLeaderboardAsync(Guid competitionId, CancellationToken ct = default) =>
        await db.Set<LeaderboardEntry>().AsNoTracking()
            .Where(e => e.CompetitionId == competitionId)
            .OrderBy(e => e.Rank)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<NamedLeaderboardEntry>> GetLeaderboardNamedAsync(Guid competitionId, CancellationToken ct = default)
    {
        var entries = await GetLeaderboardAsync(competitionId, ct);
        var ids = entries.Select(e => e.SubmitterUserId).Distinct().ToList();
        var names = await db.Set<Domain.Engagement.UserProfile>().AsNoTracking()
            .Where(p => ids.Contains(p.UserId))
            .ToDictionaryAsync(p => p.UserId, p => p.DisplayName, ct);
        return entries
            .Select(e => new NamedLeaderboardEntry(e.Rank, e.SubmitterUserId, names.GetValueOrDefault(e.SubmitterUserId, e.SubmitterUserId), e.Score))
            .ToList();
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

    private async Task UpdateLeaderboardAsync(Competition competition, string userId, Submission submission, double score, CancellationToken ct)
    {
        var higherIsBetter = scorers.Resolve(competition.ScorerCode).HigherIsBetter;

        var entry = await db.Set<LeaderboardEntry>()
            .FirstOrDefaultAsync(e => e.CompetitionId == competition.Id && e.SubmitterUserId == userId, ct);

        if (entry is null)
        {
            db.Set<LeaderboardEntry>().Add(new LeaderboardEntry
            {
                CompetitionId = competition.Id,
                SubmitterUserId = userId,
                BestSubmissionId = submission.Id,
                Score = score,
                CreatedUtc = DateTime.UtcNow,
            });
        }
        else if ((higherIsBetter && score > entry.Score) || (!higherIsBetter && score < entry.Score))
        {
            entry.Score = score;
            entry.BestSubmissionId = submission.Id;
        }

        await db.SaveChangesAsync(ct);

        // Recompute ranks (1 = best). Ties share the lower ordinal by insertion order.
        var entries = await db.Set<LeaderboardEntry>().Where(e => e.CompetitionId == competition.Id).ToListAsync(ct);
        var ordered = higherIsBetter
            ? entries.OrderByDescending(e => e.Score).ToList()
            : entries.OrderBy(e => e.Score).ToList();

        for (var i = 0; i < ordered.Count; i++)
        {
            ordered[i].Rank = i + 1;
        }

        // Relayed to the competition's SignalR group so open leaderboards refresh live.
        await outbox.EnqueueAsync(new LeaderboardUpdatedEvent(competition.Id), ct);
        await db.SaveChangesAsync(ct);
    }
}
