using System.Text;
using FluentAssertions;
using Microsoft.ML;
using Microsoft.ML.AutoML;
using Xunit;

namespace Beep.KocAiCommunity.UnitTests;

/// <summary>Probe: train → save model bytes → (fresh context) load → predict on a new CSV.</summary>
[Collection(MlTrainingCollection.Name)]
public class InferenceProbeTests
{
    [Fact]
    public async Task Train_save_load_and_predict()
    {
        var trainPath = Path.Combine(Path.GetTempPath(), $"koc-inf-train-{Guid.NewGuid():N}.csv");
        var inferPath = Path.Combine(Path.GetTempPath(), $"koc-inf-new-{Guid.NewGuid():N}.csv");

        var train = new StringBuilder("x1,x2,label\n");
        for (var i = 0; i < 60; i++)
        {
            train.Append($"{7 + (i % 3)},{7 + ((i / 3) % 3)},true\n");
            train.Append($"{i % 3},{(i / 3) % 3},false\n");
        }
        await File.WriteAllTextAsync(trainPath, train.ToString());
        // New rows to score — same columns (label present but ignored for prediction).
        await File.WriteAllTextAsync(inferPath, "x1,x2,label\n9,9,false\n0,0,false\n8,7,false\n");

        byte[] modelBytes = await Task.Run(() =>
        {
            var ml = new MLContext(seed: 1);
            var cols = ml.Auto().InferColumns(trainPath, labelColumnName: "label", groupColumns: false);
            var loader = ml.Data.CreateTextLoader(cols.TextLoaderOptions);
            var data = loader.Load(trainPath);
            var experiment = ml.Auto().CreateBinaryClassificationExperiment(new BinaryExperimentSettings
            {
                MaxExperimentTimeInSeconds = 5,
                OptimizingMetric = BinaryClassificationMetric.Accuracy,
            });
            var result = experiment.Execute(data, cols.ColumnInformation);
            using var ms = new MemoryStream();
            ml.Model.Save(result.BestRun.Model, data.Schema, ms);
            return ms.ToArray();
        });

        // Inference in a fresh context (as a separate process would).
        var predictions = await Task.Run(() =>
        {
            var ml = new MLContext();
            var model = ml.Model.Load(new MemoryStream(modelBytes), out _);
            var cols = ml.Auto().InferColumns(inferPath, labelColumnName: "label", groupColumns: false);
            var loader = ml.Data.CreateTextLoader(cols.TextLoaderOptions);
            var scored = model.Transform(loader.Load(inferPath));
            var preview = scored.Preview(maxRows: 10);
            return preview.RowView
                .Select(r => r.Values.FirstOrDefault(v => v.Key == "PredictedLabel").Value)
                .ToList();
        });

        File.Delete(trainPath);
        File.Delete(inferPath);

        predictions.Should().HaveCount(3);
        predictions[0].Should().Be(true);   // 9,9 → high → true
        predictions[1].Should().Be(false);  // 0,0 → low → false
    }
}
