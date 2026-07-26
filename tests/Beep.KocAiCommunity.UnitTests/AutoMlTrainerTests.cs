using System.Globalization;
using System.Text;
using Beep.KocAiCommunity.Application.ML;
using Beep.KocAiCommunity.ML;
using FluentAssertions;
using Xunit;

namespace Beep.KocAiCommunity.UnitTests;

[Collection(MlTrainingCollection.Name)]
public class AutoMlTrainerTests
{
    private static MemoryStream Csv(string content) => new(Encoding.UTF8.GetBytes(content));

    [Fact]
    public async Task Binary_classification_reports_accuracy()
    {
        var sb = new StringBuilder("x1,x2,label\n");
        for (var i = 0; i < 60; i++)
        {
            sb.Append($"{7 + (i % 3)},{7 + ((i / 3) % 3)},true\n");
            sb.Append($"{i % 3},{(i / 3) % 3},false\n");
        }

        var result = await new AutoMlTrainer().TrainAsync(MlTaskType.BinaryClassification, Csv(sb.ToString()), "label", 5);

        result.Task.Should().Be("BinaryClassification");
        result.PrimaryMetric.Should().Be("Accuracy");
        result.PrimaryValue.Should().BeGreaterThan(0.8);
        result.RowCount.Should().Be(120);
    }

    [Fact]
    public async Task Id_column_is_not_used_as_a_feature()
    {
        // 'id' perfectly (and threshold-learnably) encodes the label; the real features are constant, so
        // they carry no signal. If AutoML trained on the id it would score ~perfect — but the id is a row
        // key, not a feature, so it's excluded and accuracy must stay at chance.
        var sb = new StringBuilder("id,x1,x2,label\n");
        for (var i = 0; i < 120; i++)
        {
            var label = i % 2 == 0;
            var id = label ? 10000 + i : i; // id > 5000 ⟺ true — a clean threshold leak, and unique per row
            sb.Append($"{id},1,1,{(label ? "true" : "false")}\n");
        }

        var result = await new AutoMlTrainer().TrainAsync(MlTaskType.BinaryClassification, Csv(sb.ToString()), "label", 5);

        result.PrimaryValue.Should().BeLessThan(0.7, "the id must be excluded, leaving only signal-free features");
    }

    [Fact]
    public async Task Regression_reports_r_squared()
    {
        // y = 2*x1 + x2 (perfectly linear).
        var sb = new StringBuilder("x1,x2,y\n");
        for (var i = 0; i < 120; i++)
        {
            var x1 = i % 12;
            var x2 = (i * 5) % 9;
            sb.Append(string.Create(CultureInfo.InvariantCulture, $"{x1},{x2},{(2 * x1) + x2}\n"));
        }

        var result = await new AutoMlTrainer().TrainAsync(MlTaskType.Regression, Csv(sb.ToString()), "y", 6);

        result.Task.Should().Be("Regression");
        result.PrimaryMetric.Should().Be("RSquared");
        result.PrimaryValue.Should().BeGreaterThan(0.8);
    }

    [Fact]
    public async Task Multiclass_classification_reports_micro_accuracy()
    {
        // Three classes cleanly separated by x1.
        var sb = new StringBuilder("x1,x2,label\n");
        for (var i = 0; i < 150; i++)
        {
            var x1 = i % 3 == 0 ? 1 : i % 3 == 1 ? 5 : 9;
            var cls = x1 == 1 ? "A" : x1 == 5 ? "B" : "C";
            sb.Append($"{x1},{i % 4},{cls}\n");
        }

        var result = await new AutoMlTrainer().TrainAsync(MlTaskType.MulticlassClassification, Csv(sb.ToString()), "label", 6);

        result.Task.Should().Be("MulticlassClassification");
        result.PrimaryMetric.Should().Be("MicroAccuracy");
        result.PrimaryValue.Should().BeGreaterThan(0.7);
    }
}
