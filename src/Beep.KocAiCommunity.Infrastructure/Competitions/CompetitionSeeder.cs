using System.Text;
using Beep.KocAiCommunity.Application.Storage;
using Beep.KocAiCommunity.Domain.Common;
using Beep.KocAiCommunity.Domain.Competitions;
using Beep.KocAiCommunity.Domain.Organization;
using Beep.KocAiCommunity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Beep.KocAiCommunity.Infrastructure.Competitions;

/// <summary>
/// Development-only seed: one company-wide demo competition, complete with participant-visible
/// training data, an evaluation feature set, and the hidden answer key — so a Studio pipeline can
/// be submitted end to end and land on the leaderboard.
/// </summary>
public static class CompetitionSeeder
{
    private const string ClassificationTitle = "Well Integrity — Failure Prediction (Demo)";
    private const string RegressionTitle = "Production Forecast — Daily Oil Rate (Demo)";

    public static async Task SeedDemoAsync(KocDbContext db, IArtifactService artifacts, CancellationToken ct = default)
    {
        var stamp = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // Learn ↔ compete tie-in: point the demos at the hands-on "Solve a real problem" track.
        var recommendedTrackId = await db.Set<Domain.Learning.LearningTrack>()
            .Where(t => t.Title == "Solve a real problem").Select(t => (Guid?)t.Id).FirstOrDefaultAsync(ct);

        if (!await db.Set<Competition>().AnyAsync(c => c.Title == ClassificationTitle, ct))
        {
            var (training, evaluation, answerKey) = BuildClassificationData();
            await AddCompetitionAsync(db, artifacts, new DemoSpec(
                ClassificationTitle,
                "Predict whether a well needs intervention from two sensor features. Build your pipeline in the "
                + "Studio node editor, then submit it — you are scored on a hidden test set shared by all competitors, "
                + "so your feature-engineering and algorithm choices decide your rank.",
                "accuracy", "label", "BinaryClassification", training, evaluation, answerKey, recommendedTrackId), stamp, ct);
        }

        if (!await db.Set<Competition>().AnyAsync(c => c.Title == RegressionTitle, ct))
        {
            var (training, evaluation, answerKey) = BuildRegressionData();
            await AddCompetitionAsync(db, artifacts, new DemoSpec(
                RegressionTitle,
                "Forecast a well's daily oil rate from choke size and tubing pressure. A regression challenge scored "
                + "on RMSE (lower is better) against a hidden test set — tune your pipeline to minimise the error.",
                "rmse", "oil_rate", "Regression", training, evaluation, answerKey, recommendedTrackId), stamp, ct);
        }

        await db.SaveChangesAsync(ct);
    }

    private sealed record DemoSpec(
        string Title, string Description, string Scorer, string LabelColumn, string TaskType,
        string Training, string Evaluation, string AnswerKey, Guid? RecommendedTrackId = null);

    private static async Task AddCompetitionAsync(KocDbContext db, IArtifactService artifacts, DemoSpec spec, DateTime stamp, CancellationToken ct)
    {
        var slug = spec.TaskType.ToLowerInvariant();
        var trainingRef = await Save(artifacts, spec.Training, $"{slug}/training.csv", ct);
        var evalRef = await Save(artifacts, spec.Evaluation, $"{slug}/evaluation.csv", ct);
        var keyRef = await Save(artifacts, spec.AnswerKey, $"{slug}/answer-key.csv", ct);

        db.Set<Competition>().Add(new Competition
        {
            Title = spec.Title,
            Description = spec.Description,
            Status = "active",
            VisibilityScope = VisibilityScope.Company,
            VisibilityOrgUnitId = Guid.Empty,
            SubmissionQuotaPerDay = 25,
            ScorerCode = spec.Scorer,
            TrainingDatasetArtifactId = trainingRef.Id,
            EvaluationArtifactId = evalRef.Id,
            AnswerKeyArtifactId = keyRef.Id,
            LabelColumn = spec.LabelColumn,
            IdColumn = "id",
            TaskType = spec.TaskType,
            RecommendedTrackId = spec.RecommendedTrackId,
            CreatedByUserId = "dev-lead",
            CreatedUtc = stamp,
        });
    }

    private static async Task<Domain.Storage.ArtifactReference> Save(IArtifactService artifacts, string csv, string name, CancellationToken ct)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        return await artifacts.SaveAsync(stream, $"competitions/demo/{name}", "text/csv", KocDataClassification.Internal, ct);
    }

    private static (string Training, string Evaluation, string AnswerKey) BuildClassificationData()
    {
        // Separable rule: high sensor readings ⇒ intervention needed (true).
        var training = new StringBuilder("id,pressure,vibration,label\n");
        for (var i = 0; i < 90; i++)
        {
            training.Append($"tr{i}p,{8 + (i % 3)},{8 + ((i / 3) % 3)},true\n");
            training.Append($"tr{i}n,{i % 3},{(i / 3) % 3},false\n");
        }

        var evaluation = new StringBuilder("id,pressure,vibration\n");
        var answerKey = new StringBuilder("id,label\n");
        for (var k = 0; k < 40; k++)
        {
            var needsIntervention = k % 2 == 0;
            var pressure = needsIntervention ? 8 + (k % 3) : k % 3;
            var vibration = needsIntervention ? 8 + (k % 2) : k % 2;
            evaluation.Append($"e{k},{pressure},{vibration}\n");
            answerKey.Append($"e{k},{(needsIntervention ? "true" : "false")}\n");
        }

        return (training.ToString(), evaluation.ToString(), answerKey.ToString());
    }

    private static (string Training, string Evaluation, string AnswerKey) BuildRegressionData()
    {
        // Linear target: oil_rate ≈ 40·choke + 6·tubing_pressure (deterministic, so a fitted model scores well).
        var training = new StringBuilder("id,choke,tubing_pressure,oil_rate\n");
        for (var i = 0; i < 120; i++)
        {
            var choke = 1 + (i % 8);
            var tubing = 20 + (i % 25);
            training.Append($"tr{i},{choke},{tubing},{(40 * choke) + (6 * tubing)}\n");
        }

        var evaluation = new StringBuilder("id,choke,tubing_pressure\n");
        var answerKey = new StringBuilder("id,oil_rate\n");
        for (var k = 0; k < 40; k++)
        {
            var choke = 1 + (k % 7);
            var tubing = 22 + (k % 20);
            evaluation.Append($"e{k},{choke},{tubing}\n");
            answerKey.Append($"e{k},{(40 * choke) + (6 * tubing)}\n");
        }

        return (training.ToString(), evaluation.ToString(), answerKey.ToString());
    }
}
