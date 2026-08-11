using System.Globalization;
using System.Text;
using Beep.KocAiCommunity.Application.Competitions;
using Beep.KocAiCommunity.Application.ML;
using Beep.KocAiCommunity.Application.Storage;
using Beep.KocAiCommunity.Contracts.Workflow;
using Beep.KocAiCommunity.Domain.Common;
using Beep.KocAiCommunity.Domain.Competitions;
using Beep.KocAiCommunity.Domain.Storage;
using Beep.KocAiCommunity.Infrastructure.Competitions;
using Beep.KocAiCommunity.Infrastructure.Persistence;
using Beep.KocAiCommunity.ML.Nodes;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Xunit.Abstractions;

namespace Beep.KocAiCommunity.UnitTests;

/// <summary>
/// Proof that the seeded competitions are winnable, not merely well-formed: every one is put through
/// the same path a member's submission takes — run an ordinary Studio pipeline over the competition's
/// own training data, predict the evaluation set, score against the hidden key — and the result has to
/// beat the trivial baseline (guess the majority class, or predict the mean).
/// <para>
/// A generated dataset whose relationship was too weak, too noisy, or accidentally absent would pass
/// every structural check in <see cref="SeededCompetitionDataTests"/> and still leave the leaderboard
/// ranking coin flips. This is the test that would notice.
/// </para>
/// </summary>
// Serialised with the other AutoML tests: training gets a fixed wall-clock budget, so parallel runs
// starve each other of cores and fail on a worse model than the same test produces alone.
[Collection(MlTrainingCollection.Name)]
public sealed class SeededCompetitionLearnabilityTests(ITestOutputHelper output) : IDisposable
{
    private readonly SqliteConnection _connection = NewConnection();
    private readonly CapturingArtifacts _artifacts = new();
    private KocDbContext? _db;

    private static SqliteConnection NewConnection()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        return connection;
    }

    public void Dispose()
    {
        _db?.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task Every_seeded_competition_can_be_beaten_by_an_ordinary_pipeline()
    {
        _db = new KocDbContext(new DbContextOptionsBuilder<KocDbContext>().UseSqlite(_connection).Options);
        await _db.Database.EnsureCreatedAsync();
        await CompetitionSeeder.SeedCompetitionsAsync(_db, _artifacts);

        var scorers = new IScoringPlugin[] { new AccuracyScorer(), new RmseScorer(), new AucScorer() };
        var failures = new List<string>();

        foreach (var competition in await _db.Set<Competition>().OrderBy(c => c.Title).ToListAsync())
        {
            var training = _artifacts.Content[competition.TrainingDatasetArtifactId!.Value];
            var evaluation = _artifacts.Content[competition.EvaluationArtifactId!.Value];
            var answerKey = _artifacts.Content[competition.AnswerKeyArtifactId!.Value];

            var scorer = scorers.Single(s => s.Code == competition.ScorerCode);
            var submission = await NewExecutor().PredictAsync(
                Pipeline(), competition.LabelColumn, competition.IdColumn, Task(competition),
                Csv(training), Csv(evaluation));

            var score = await scorer.ScoreAsync(Csv(submission), Csv(answerKey), competition.IdColumn);
            var baseline = Baseline(competition, training, answerKey);
            var beat = scorer.HigherIsBetter ? score > baseline : score < baseline;

            output.WriteLine(
                $"{(beat ? "PASS" : "FAIL")}  {competition.ScorerCode,-8} {score,10:0.0000}  "
                + $"(baseline {baseline,10:0.0000})  {competition.TaskType,-25} {competition.Title}");

            if (!beat)
            {
                failures.Add($"{competition.Title}: {competition.ScorerCode} {score:0.0000} did not beat the baseline {baseline:0.0000}");
            }
        }

        failures.Should().BeEmpty();
    }

    /// <summary>What a member gets for free, and therefore what the data has to be worth more than.</summary>
    private static double Baseline(Competition competition, string training, string answerKey)
    {
        var key = Column(answerKey, competition.LabelColumn);

        if (competition.ScorerCode == "rmse")
        {
            // Predict the training mean for every row — the no-model regression answer.
            var mean = Column(training, competition.LabelColumn)
                .Select(v => double.Parse(v, CultureInfo.InvariantCulture)).Average();
            var actual = key.Select(v => double.Parse(v, CultureInfo.InvariantCulture)).ToList();
            return Math.Sqrt(actual.Sum(a => (a - mean) * (a - mean)) / actual.Count);
        }

        if (competition.ScorerCode == "auc")
        {
            return 0.5;   // ranking at random
        }

        // Always answer with the commonest class in the training set.
        var majority = Column(training, competition.LabelColumn)
            .GroupBy(v => v, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count()).First().Key;
        return (double)key.Count(v => v.Equals(majority, StringComparison.OrdinalIgnoreCase)) / key.Count;
    }

    private static MlTaskType Task(Competition competition) =>
        string.Equals(competition.TaskType, "Forecasting", StringComparison.OrdinalIgnoreCase)
            ? MlTaskType.Regression
            : Enum.Parse<MlTaskType>(competition.TaskType, ignoreCase: true);

    // The pipeline a member would build on their first afternoon: read the data, fill the gaps in it,
    // normalise, hold some back, fit, evaluate. Nothing bespoke per competition — that is the point.
    //
    // Replace missing is in here because the datasets are deliberately dirty, and every one of them has
    // to be winnable by somebody who does the obvious thing about that. It is NOT a claim that this is
    // the best answer: the unit columns still need a Compute column node, and the -999 sentinels a
    // Filter rows, so the scores below are a floor for a careful member rather than a ceiling.
    private static WorkflowDefinition Pipeline() => new()
    {
        Name = "ordinary",
        Nodes =
        [
            new() { Id = "d", Kind = "dataset" },
            new() { Id = "rm", Kind = "replace-missing" },
            new() { Id = "oh", Kind = "one-hot" },
            new() { Id = "nz", Kind = "normalize" },
            new() { Id = "sp", Kind = "split" },
            new() { Id = "tr", Kind = "train" },
            new() { Id = "ev", Kind = "evaluate" },
        ],
        Edges = [new("d", "rm"), new("rm", "oh"), new("oh", "nz"), new("nz", "sp"), new("sp", "tr"), new("tr", "ev")],
    };

    private static PluginNodeExecutor NewExecutor()
    {
        var handlers = typeof(PluginNodeExecutor).Assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false } && typeof(IPipelineNodeHandler).IsAssignableFrom(t))
            .Select(t => (IPipelineNodeHandler)Activator.CreateInstance(t)!);
        return new PluginNodeExecutor(new PluginNodeRegistry(handlers));
    }

    private static MemoryStream Csv(string text) => new(Encoding.UTF8.GetBytes(text));

    private static List<string> Column(string csv, string name)
    {
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var header = lines[0].Trim().Split(',');
        var index = Array.FindIndex(header, h => h.Equals(name, StringComparison.OrdinalIgnoreCase));
        return [.. lines.Skip(1).Select(l => l.Trim().Split(',')[index])];
    }

    private sealed class CapturingArtifacts : IArtifactService
    {
        public Dictionary<Guid, string> Content { get; } = [];

        public Task<ArtifactReference> SaveAsync(
            Stream content, string logicalPath, string contentType, KocDataClassification classification, CancellationToken ct = default)
        {
            using var reader = new StreamReader(content);
            var reference = new ArtifactReference { Id = Guid.NewGuid(), LogicalPath = logicalPath, ContentType = contentType };
            Content[reference.Id] = reader.ReadToEnd();
            return System.Threading.Tasks.Task.FromResult(reference);
        }

        public Task<Stream> OpenReadAsync(Guid artifactReferenceId, CancellationToken ct = default) =>
            System.Threading.Tasks.Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes(Content[artifactReferenceId])));
    }
}
