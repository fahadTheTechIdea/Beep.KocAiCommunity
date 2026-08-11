using System.Globalization;
using Beep.KocAiCommunity.Application.Storage;
using Beep.KocAiCommunity.Domain.Common;
using Beep.KocAiCommunity.Domain.Competitions;
using Beep.KocAiCommunity.Domain.Storage;
using Beep.KocAiCommunity.Infrastructure.Competitions;
using Beep.KocAiCommunity.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Beep.KocAiCommunity.UnitTests;

/// <summary>
/// The competitions ship with the platform, so a member's first experience of it is submitting to one.
/// That only works if every seeded dataset is actually scorable: the evaluation set must withhold the
/// label, the answer key must line up with it by id, and the classes must be balanced enough that the
/// metric means something. A generator edit that quietly breaks one of those would otherwise only be
/// discovered by whoever submits first.
/// </summary>
public sealed class SeededCompetitionDataTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly KocDbContext _db;
    private readonly CapturingArtifacts _artifacts = new();

    public SeededCompetitionDataTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _db = new KocDbContext(new DbContextOptionsBuilder<KocDbContext>().UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task Every_seeded_competition_has_a_scorable_dataset()
    {
        await CompetitionSeeder.SeedCompetitionsAsync(_db, _artifacts);

        var competitions = await _db.Set<Competition>().ToListAsync();
        competitions.Should().HaveCountGreaterThanOrEqualTo(15);

        foreach (var competition in competitions)
        {
            var reason = competition.Title;

            var training = Csv(competition.TrainingDatasetArtifactId);
            var evaluation = Csv(competition.EvaluationArtifactId);
            var answerKey = Csv(competition.AnswerKeyArtifactId);

            // Enough rows to fit something, and an honest hold-out.
            training.Rows.Should().HaveCountGreaterThanOrEqualTo(200, reason);
            evaluation.Rows.Should().HaveCountGreaterThanOrEqualTo(80, reason);

            // The id column is present everywhere and unique — the scorers align on it.
            foreach (var file in new[] { training, evaluation, answerKey })
            {
                file.Header.Should().Contain(competition.IdColumn, reason);
                file.Column(competition.IdColumn).Should().OnlyHaveUniqueItems(reason);
            }

            // The answer key covers exactly the evaluation rows: a missing id scores as a miss, and an
            // extra one would score a row nobody was asked to predict.
            answerKey.Column(competition.IdColumn).Should()
                .BeEquivalentTo(evaluation.Column(competition.IdColumn), reason);

            // The label is in the key, and — the part that matters — NOT in the evaluation features.
            answerKey.Header.Should().Contain(competition.LabelColumn, reason);
            evaluation.Header.Should().NotContain(competition.LabelColumn, reason);

            // Anomaly detection trains unlabelled; everything else needs its label to learn from.
            if (competition.TaskType == "AnomalyDetection")
            {
                training.Header.Should().NotContain(competition.LabelColumn, reason);
            }
            else
            {
                training.Header.Should().Contain(competition.LabelColumn, reason);
            }

            // Holes are deliberate — cleaning is part of the challenge — but they are bounded, and they
            // never touch the two columns that make a row scorable at all.
            foreach (var file in new[] { training, evaluation })
            {
                file.Column(competition.IdColumn).Should().NotContain(string.Empty, reason);

                var cells = file.Rows.SelectMany(r => r).ToList();
                var missing = (double)cells.Count(c => c.Length == 0) / cells.Count;
                missing.Should().BeLessThan(0.15, $"{reason} should be dirty, not unusable");
            }

            if (competition.TaskType != "AnomalyDetection")
            {
                training.Column(competition.LabelColumn).Should().NotContain(string.Empty, reason);
            }

            // The answer key is ground truth. Nothing is injected into it, ever.
            answerKey.Rows.SelectMany(r => r).Should().NotContain(string.Empty, reason);

            AssertLabelsAreUsable(competition, training, answerKey);
        }
    }

    /// <summary>
    /// A dataset can be perfectly well-formed and still make a worthless competition — a regression
    /// target that never moves, or a class that appears twice in a thousand rows. This is where that is
    /// caught.
    /// </summary>
    private static void AssertLabelsAreUsable(Competition competition, CsvFile training, CsvFile answerKey)
    {
        var reason = competition.Title;

        // Score against the key, since that is what every submission is measured on.
        var keyLabels = answerKey.Column(competition.LabelColumn);

        switch (competition.TaskType)
        {
            case "Regression":
            case "Forecasting":
            {
                var values = keyLabels.Select(v => double.Parse(v, CultureInfo.InvariantCulture)).ToList();
                values.Should().OnlyContain(v => double.IsFinite(v), reason);

                // A target with no spread makes RMSE meaningless — everyone would tie at zero.
                var mean = values.Average();
                var spread = Math.Sqrt(values.Sum(v => (v - mean) * (v - mean)) / values.Count);
                spread.Should().BeGreaterThan(Math.Abs(mean) * 0.02, reason);
                break;
            }

            case "AnomalyDetection":
            {
                // AUC needs both classes present, and the positives should stay rare — that rarity is
                // the reason the metric is AUC and not accuracy.
                var positives = keyLabels.Count(v => v is "1" or "true");
                positives.Should().BeGreaterThan(5, reason);
                ((double)positives / keyLabels.Count).Should().BeInRange(0.05, 0.35, reason);
                break;
            }

            default:
            {
                // Classification: every class in the key must also be learnable from the training set,
                // with enough examples of the rarest one to fit anything at all.
                var trainingLabels = training.Column(competition.LabelColumn);
                var trainingClasses = trainingLabels.GroupBy(v => v, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

                var keyClasses = keyLabels.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                keyClasses.Should().HaveCountGreaterThanOrEqualTo(2, reason);
                keyClasses.Should().OnlyContain(c => trainingClasses.ContainsKey(c), reason);

                trainingClasses.Values.Min().Should().BeGreaterThanOrEqualTo(20, reason);

                // Guessing the majority class must not already be a good score, or the leaderboard
                // would rank nobody's modelling.
                var majority = (double)trainingClasses.Values.Max() / trainingLabels.Count;
                majority.Should().BeLessThan(0.80, reason);
                break;
            }
        }
    }

    private CsvFile Csv(Guid? artifactId)
    {
        artifactId.Should().NotBeNull();
        return CsvFile.Parse(_artifacts.Content[artifactId!.Value]);
    }

    private sealed record CsvFile(string[] Header, List<string[]> Rows)
    {
        public static CsvFile Parse(string text)
        {
            var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            return new CsvFile(lines[0].Trim().Split(','), [.. lines.Skip(1).Select(l => l.Trim().Split(','))]);
        }

        public List<string> Column(string name)
        {
            var index = Array.FindIndex(Header, h => h.Equals(name, StringComparison.OrdinalIgnoreCase));
            index.Should().BeGreaterThanOrEqualTo(0, $"'{name}' should be one of: {string.Join(", ", Header)}");
            return [.. Rows.Select(r => r[index])];
        }
    }

    /// <summary>Keeps every CSV the seeder saves, so the test can read back what participants download.</summary>
    private sealed class CapturingArtifacts : IArtifactService
    {
        public Dictionary<Guid, string> Content { get; } = [];

        public Task<ArtifactReference> SaveAsync(
            Stream content, string logicalPath, string contentType, KocDataClassification classification, CancellationToken ct = default)
        {
            using var reader = new StreamReader(content);
            var reference = new ArtifactReference { Id = Guid.NewGuid(), LogicalPath = logicalPath, ContentType = contentType };
            Content[reference.Id] = reader.ReadToEnd();
            return Task.FromResult(reference);
        }

        public Task<Stream> OpenReadAsync(Guid artifactReferenceId, CancellationToken ct = default) =>
            Task.FromResult<Stream>(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(Content[artifactReferenceId])));
    }
}
