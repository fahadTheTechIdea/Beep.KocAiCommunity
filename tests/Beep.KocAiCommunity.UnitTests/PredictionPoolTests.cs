using System.Text;
using Beep.KocAiCommunity.Application.ML;
using Beep.KocAiCommunity.ML;
using FluentAssertions;
using Xunit;

namespace Beep.KocAiCommunity.UnitTests;

/// <summary>
/// The dynamic-schema prediction pool: train → capture bytes → score new rows, plus caching (load
/// once, reuse) and eviction (reload after evict).
/// </summary>
[Collection(MlTrainingCollection.Name)]
public class PredictionPoolTests
{
    private static async Task<byte[]> TrainSeparableModelAsync()
    {
        var csv = new StringBuilder("x1,x2,label\n");
        for (var i = 0; i < 60; i++)
        {
            csv.Append($"{7 + (i % 3)},{7 + ((i / 3) % 3)},true\n");   // high → true
            csv.Append($"{i % 3},{(i / 3) % 3},false\n");             // low  → false
        }

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv.ToString()));
        var captured = await new AutoMlTrainer().TrainAndCaptureAsync(MlTaskType.BinaryClassification, stream, "label", maxSeconds: 5);
        captured.FeatureStatsJson.Should().Contain("x1").And.Contain("x2");
        captured.ModelBytes.Should().NotBeEmpty();
        return captured.ModelBytes;
    }

    [Fact]
    public async Task Scores_new_rows_against_the_model_schema()
    {
        var bytes = await TrainSeparableModelAsync();
        var pool = new AutoMlPredictionPool();

        var rows = new IReadOnlyDictionary<string, string>[]
        {
            new Dictionary<string, string> { ["x1"] = "9", ["x2"] = "9" }, // high → true
            new Dictionary<string, string> { ["x1"] = "0", ["x2"] = "0" }, // low  → false
        };

        var result = await pool.PredictAsync(Guid.NewGuid(), _ => Task.FromResult(bytes), "label", rows);

        result.Predictions.Should().HaveCount(2);
        result.Predictions[0].PredictedLabel.Should().Be("true");
        result.Predictions[1].PredictedLabel.Should().Be("false");
        result.Predictions[0].Probability.Should().BeGreaterThan(result.Predictions[1].Probability!.Value);
    }

    [Fact]
    public async Task Caches_the_model_and_reloads_after_eviction()
    {
        var bytes = await TrainSeparableModelAsync();
        var pool = new AutoMlPredictionPool();
        var id = Guid.NewGuid();
        var loads = 0;
        Task<byte[]> Loader(CancellationToken _) { loads++; return Task.FromResult(bytes); }

        var row = new IReadOnlyDictionary<string, string>[] { new Dictionary<string, string> { ["x1"] = "9", ["x2"] = "9" } };

        await pool.PredictAsync(id, Loader, "label", row);
        await pool.PredictAsync(id, Loader, "label", row);
        loads.Should().Be(1); // second call is served from the cache

        pool.Evict(id);
        await pool.PredictAsync(id, Loader, "label", row);
        loads.Should().Be(2); // reloaded after eviction
    }
}
