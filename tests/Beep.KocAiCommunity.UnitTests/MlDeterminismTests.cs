using System.Text;
using Beep.KocAiCommunity.Application.ML;
using Beep.KocAiCommunity.ML;
using FluentAssertions;
using Xunit;

namespace Beep.KocAiCommunity.UnitTests;

[Collection(MlTrainingCollection.Name)]
public class MlDeterminismTests
{
    private static Stream Csv()
    {
        var sb = new StringBuilder("x1,x2,label\n");
        for (var i = 0; i < 60; i++)
        {
            sb.Append($"{7 + (i % 3)},{7 + ((i / 3) % 3)},true\n");
            sb.Append($"{i % 3},{(i / 3) % 3},false\n");
        }

        return new MemoryStream(Encoding.UTF8.GetBytes(sb.ToString()));
    }

    [Fact]
    public async Task Training_with_a_fixed_seed_is_reproducible()
    {
        var trainer = new AutoMlTrainer();
        var a = await trainer.TrainAsync(MlTaskType.BinaryClassification, Csv(), "label", maxSeconds: 5);
        var b = await trainer.TrainAsync(MlTaskType.BinaryClassification, Csv(), "label", maxSeconds: 5);

        // Same seed + same data → identical winning algorithm and metrics.
        b.Algorithm.Should().Be(a.Algorithm);
        b.PrimaryValue.Should().Be(a.PrimaryValue);
        b.SecondaryValue.Should().Be(a.SecondaryValue);
    }
}
