using System.Globalization;
using System.Text;
using Beep.KocAiCommunity.Application.Storage;
using Beep.KocAiCommunity.Domain.Common;
using Beep.KocAiCommunity.Domain.Competitions;
using Beep.KocAiCommunity.Domain.Organization;
using Beep.KocAiCommunity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Beep.KocAiCommunity.Infrastructure.Competitions;

/// <summary>
/// Development-only seed: a curated set of company-wide demo competitions, each complete with a
/// participant-visible training set, an evaluation feature set, and the hidden answer key — so a
/// Studio pipeline can be submitted end to end and land on the leaderboard. Includes the famous
/// Titanic starter challenge (accessible to anyone) alongside real oil-&amp;-gas problems (binary,
/// multiclass, and regression). Datasets are generated deterministically and carry a real, learnable
/// signal so a fitted model scores well. Idempotent per-title.
/// </summary>
public static class CompetitionSeeder
{
    private const string TitanicTitle = "Titanic — Who Survives? (Starter Challenge)";
    private const string WellIntegrityTitle = "Well Integrity — Intervention Needed? (Demo)";
    private const string EspTitle = "ESP Pump Failure — Predictive Maintenance (Demo)";
    private const string ProductionTitle = "Production Forecast — Daily Oil Rate, bopd (Demo)";
    private const string FaciesTitle = "Rock Facies from Well Logs (Demo)";

    public static async Task SeedDemoAsync(KocDbContext db, IArtifactService artifacts, CancellationToken ct = default)
    {
        var stamp = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // Learn ↔ compete tie-ins.
        var starterTrack = await TrackId(db, "AI for Everyone — Start Here", ct)
            ?? await TrackId(db, "Getting started with data", ct);
        var solveTrack = await TrackId(db, "Solve a real problem", ct);

        await MaybeAddAsync(db, artifacts, stamp, ct, new DemoSpec(
            TitanicTitle,
            "The world's most famous first machine-learning problem — perfect for a first try, no oil-&-gas "
            + "knowledge needed. From a passenger's details (class, sex, age, fare, family aboard), predict "
            + "who survived. Build a pipeline in the Studio, submit it, and see your accuracy on a hidden test "
            + "set. A great way to *see the idea* behind the whole platform.",
            "accuracy", "survived", "BinaryClassification", BuildTitanic(), starterTrack, IsFeatured: true));

        await MaybeAddAsync(db, artifacts, stamp, ct, new DemoSpec(
            WellIntegrityTitle,
            "Predict whether a well needs intervention from routine surveillance readings (casing & annulus "
            + "pressure, temperature, water cut, well age). A binary challenge scored on accuracy against a "
            + "hidden test set — your feature choices and algorithm decide your rank.",
            "accuracy", "label", "BinaryClassification", BuildWellIntegrity(), solveTrack));

        await MaybeAddAsync(db, artifacts, stamp, ct, new DemoSpec(
            EspTitle,
            "Predictive maintenance for electric submersible pumps. From intake pressure, motor temperature, "
            + "vibration, current, flow rate, and runtime hours, flag pumps likely to fail so they can be "
            + "serviced on plan — not in an emergency. Binary classification, scored on accuracy.",
            "accuracy", "failure", "BinaryClassification", BuildEsp(), solveTrack));

        await MaybeAddAsync(db, artifacts, stamp, ct, new DemoSpec(
            ProductionTitle,
            "Forecast a well's daily oil rate (bopd) from choke size, tubing head pressure, water cut, gas rate, "
            + "and days online. A regression challenge scored on RMSE (lower is better) against a hidden test "
            + "set — tune your pipeline to minimise the error.",
            "rmse", "oil_rate", "Regression", BuildProduction(), solveTrack));

        await MaybeAddAsync(db, artifacts, stamp, ct, new DemoSpec(
            FaciesTitle,
            "Classify the rock type (sandstone, shale, limestone, or dolomite) from well-log measurements — "
            + "gamma ray, resistivity, bulk density, neutron porosity, and photoelectric factor. A multiclass "
            + "challenge scored on accuracy; a classic subsurface interpretation task, done with ML.",
            "accuracy", "facies", "MulticlassClassification", BuildFacies(), solveTrack));

        await db.SaveChangesAsync(ct);
    }

    private static Task<Guid?> TrackId(KocDbContext db, string title, CancellationToken ct) =>
        db.Set<Domain.Learning.LearningTrack>().Where(t => t.Title == title).Select(t => (Guid?)t.Id).FirstOrDefaultAsync(ct);

    private sealed record DemoSpec(
        string Title, string Description, string Scorer, string LabelColumn, string TaskType,
        (string Training, string Evaluation, string AnswerKey) Data, Guid? RecommendedTrackId = null, bool IsFeatured = false);

    private static async Task MaybeAddAsync(KocDbContext db, IArtifactService artifacts, DateTime stamp, CancellationToken ct, DemoSpec spec)
    {
        if (await db.Set<Competition>().AnyAsync(c => c.Title == spec.Title, ct))
        {
            return;
        }

        var slug = Slug(spec.Title);
        var trainingRef = await Save(artifacts, spec.Data.Training, $"{slug}/training.csv", ct);
        var evalRef = await Save(artifacts, spec.Data.Evaluation, $"{slug}/evaluation.csv", ct);
        var keyRef = await Save(artifacts, spec.Data.AnswerKey, $"{slug}/answer-key.csv", ct);

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
            IsFeatured = spec.IsFeatured,
            CreatedByUserId = "dev-lead",
            CreatedUtc = stamp,
        });
    }

    private static async Task<Domain.Storage.ArtifactReference> Save(IArtifactService artifacts, string csv, string name, CancellationToken ct)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        return await artifacts.SaveAsync(stream, $"competitions/demo/{name}", "text/csv", KocDataClassification.Internal, ct);
    }

    private static string Slug(string title)
    {
        var chars = title.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray();
        return new string(chars).Trim('-');
    }

    // ------------------------------------------------------------------ generators

    // Titanic survival: features drive a realistic survival probability (women/children/1st-class first).
    private static (string, string, string) BuildTitanic()
    {
        const string header = "pclass,sex,age,sibsp,parch,fare,embarked";
        var rnd = new Random(20260101);

        (string Features, string Label) Row()
        {
            var pclass = Pick(rnd, (1, 0.24), (2, 0.21), (3, 0.55));
            var female = rnd.NextDouble() < 0.35;
            var sex = female ? "female" : "male";
            var age = (int)Math.Clamp(Gauss(rnd, pclass == 1 ? 38 : pclass == 2 ? 29 : 24, 13), 1, 80);
            var sibsp = rnd.NextDouble() < 0.68 ? 0 : rnd.NextDouble() < 0.75 ? 1 : rnd.Next(2, 6);
            var parch = rnd.NextDouble() < 0.76 ? 0 : rnd.NextDouble() < 0.75 ? 1 : rnd.Next(2, 5);
            var fare = Math.Max(4, Gauss(rnd, pclass == 1 ? 84 : pclass == 2 ? 21 : 13, pclass == 1 ? 40 : 8));
            var embarked = Pick(rnd, ("S", 0.72), ("C", 0.19), ("Q", 0.09));

            var score = -0.5
                + (female ? 2.6 : -0.6)
                + (pclass == 1 ? 1.1 : pclass == 2 ? 0.3 : -0.6)
                + (age <= 10 ? 1.1 : age >= 62 ? -0.5 : 0)
                + (fare / 80.0 * 0.5)
                - (sibsp + parch > 3 ? 0.7 : 0)
                + (Gauss(rnd, 0, 0.7));
            var survived = 1.0 / (1.0 + Math.Exp(-score)) > 0.5 ? "1" : "0";
            return ($"{pclass},{sex},{age},{sibsp},{parch},{F(fare)},{embarked}", survived);
        }

        return Emit(header, "survived", 620, 170, Row);
    }

    // Well integrity: high annulus pressure + water cut + age ⇒ intervention (true).
    private static (string, string, string) BuildWellIntegrity()
    {
        const string header = "casing_pressure,annulus_pressure,temperature,water_cut,age_years";
        var rnd = new Random(20260102);

        (string, string) Row()
        {
            var casing = Gauss(rnd, 1800, 500);
            var annulus = Math.Max(0, Gauss(rnd, 300, 220));
            var temp = Gauss(rnd, 95, 20);
            var water = Math.Clamp(Gauss(rnd, 45, 28), 0, 98);
            var age = (int)Math.Clamp(Gauss(rnd, 14, 8), 1, 35);
            var score = (annulus / 900.0 * 1.4) + (water / 100.0 * 1.2) + (age / 35.0 * 1.0) + (temp / 140.0 * 0.4) - 1.7 + Gauss(rnd, 0, 0.45);
            var label = score > 0 ? "true" : "false";
            return ($"{F(casing)},{F(annulus)},{F(temp)},{F(water)},{age}", label);
        }

        return Emit(header, "label", 520, 150, Row);
    }

    // ESP predictive maintenance: hot motor + vibration + current + runtime ⇒ failure.
    private static (string, string, string) BuildEsp()
    {
        const string header = "intake_pressure,motor_temp,vibration,current,flow_rate,runtime_hours";
        var rnd = new Random(20260103);

        (string, string) Row()
        {
            var intake = Gauss(rnd, 750, 250);
            var motor = Gauss(rnd, 150, 40);
            var vibration = Math.Max(0.05, Gauss(rnd, 2.4, 1.3));
            var current = Gauss(rnd, 65, 22);
            var flow = Math.Max(50, Gauss(rnd, 1900, 700));
            var runtime = Math.Max(50, Gauss(rnd, 4200, 2200));
            var score = (motor / 260.0 * 1.3) + (vibration / 6.0 * 1.5) + (current / 120.0 * 0.7) + (runtime / 9000.0 * 0.9) - 1.95 + Gauss(rnd, 0, 0.4);
            var failure = score > 0 ? "true" : "false";
            return ($"{F(intake)},{F(motor)},{F(vibration)},{F(current)},{F(flow)},{F(runtime)}", failure);
        }

        return Emit(header, "failure", 560, 160, Row);
    }

    // Daily oil rate: a mostly-linear response with noise (learnable regression target).
    private static (string, string, string) BuildProduction()
    {
        const string header = "choke,tubing_pressure,water_cut,gas_rate,days_online";
        var rnd = new Random(20260104);

        (string, string) Row()
        {
            var choke = (int)Math.Clamp(Gauss(rnd, 32, 14), 8, 64);
            var thp = Gauss(rnd, 1200, 500);
            var water = Math.Clamp(Gauss(rnd, 40, 25), 0, 95);
            var gas = Math.Max(0, Gauss(rnd, 1200, 700));
            var days = (int)Math.Clamp(Gauss(rnd, 900, 700), 20, 3200);
            var oil = Math.Max(0, (12 * choke) + (0.6 * thp) - (8 * water) + (0.15 * gas) - (0.05 * days) + Gauss(rnd, 0, 45));
            return ($"{choke},{F(thp)},{F(water)},{F(gas)},{days}", F(oil));
        }

        return Emit(header, "oil_rate", 560, 160, Row);
    }

    // Facies from logs: pick the rock type first, then draw log responses typical of it (clear signal).
    private static (string, string, string) BuildFacies()
    {
        const string header = "gamma_ray,resistivity,density,neutron_porosity,photoelectric";
        var rnd = new Random(20260105);

        (string, string) Row()
        {
            var facies = Pick(rnd, ("sandstone", 0.40), ("shale", 0.35), ("limestone", 0.15), ("dolomite", 0.10));
            (double gr, double res, double den, double phi, double pe) = facies switch
            {
                "sandstone" => (Gauss(rnd, 32, 15), Gauss(rnd, 35, 20), Gauss(rnd, 2.35, 0.08), Gauss(rnd, 22, 7), Gauss(rnd, 1.9, 0.3)),
                "shale" => (Gauss(rnd, 115, 22), Gauss(rnd, 6, 4), Gauss(rnd, 2.52, 0.08), Gauss(rnd, 32, 7), Gauss(rnd, 3.1, 0.5)),
                "limestone" => (Gauss(rnd, 20, 9), Gauss(rnd, 220, 120), Gauss(rnd, 2.70, 0.05), Gauss(rnd, 12, 6), Gauss(rnd, 5.0, 0.3)),
                _ => (Gauss(rnd, 26, 11), Gauss(rnd, 160, 90), Gauss(rnd, 2.84, 0.04), Gauss(rnd, 8, 5), Gauss(rnd, 3.1, 0.3)),
            };
            var features = $"{F(Math.Max(5, gr))},{F(Math.Max(0.2, res))},{F(den)},{F(Math.Clamp(phi, 0, 48))},{F(Math.Max(1, pe))}";
            return (features, facies);
        }

        return Emit(header, "facies", 640, 180, Row);
    }

    // ------------------------------------------------------------------ helpers

    /// <summary>Emits (training with label, evaluation without label, answer key) from a row generator.</summary>
    private static (string, string, string) Emit(string featureHeader, string labelColumn, int trainCount, int evalCount, Func<(string Features, string Label)> row)
    {
        var training = new StringBuilder($"id,{featureHeader},{labelColumn}\n");
        for (var i = 0; i < trainCount; i++)
        {
            var (features, label) = row();
            training.Append($"tr{i},{features},{label}\n");
        }

        var evaluation = new StringBuilder($"id,{featureHeader}\n");
        var answerKey = new StringBuilder($"id,{labelColumn}\n");
        for (var k = 0; k < evalCount; k++)
        {
            var (features, label) = row();
            evaluation.Append($"e{k},{features}\n");
            answerKey.Append($"e{k},{label}\n");
        }

        return (training.ToString(), evaluation.ToString(), answerKey.ToString());
    }

    private static double Gauss(Random r, double mean, double sd)
    {
        var u1 = 1.0 - r.NextDouble();
        var u2 = 1.0 - r.NextDouble();
        return mean + (sd * Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2));
    }

    private static T Pick<T>(Random r, params (T Value, double Weight)[] options)
    {
        var roll = r.NextDouble() * options.Sum(o => o.Weight);
        var cumulative = 0.0;
        foreach (var (value, weight) in options)
        {
            cumulative += weight;
            if (roll <= cumulative)
            {
                return value;
            }
        }

        return options[^1].Value;
    }

    private static string F(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);
}
