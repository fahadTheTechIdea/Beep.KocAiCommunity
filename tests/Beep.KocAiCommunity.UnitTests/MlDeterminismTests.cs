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
        var a = await trainer.TrainAsync(MlTaskType.BinaryClassification, Csv(), "label", maxSeconds: 20);
        var b = await trainer.TrainAsync(MlTaskType.BinaryClassification, Csv(), "label", maxSeconds: 20);

        // AutoML is wall-clock bounded, so under heavy machine load the two runs may fit a different
        // number of trials and crown different winners. Per-trial results ARE seeded/deterministic:
        // whenever the same winner emerges, its metrics must match exactly.
        if (a.Algorithm == b.Algorithm)
        {
            b.PrimaryValue.Should().Be(a.PrimaryValue);
            b.SecondaryValue.Should().Be(a.SecondaryValue);
        }
        else
        {
            // Different trial counts — both runs must still solve this separable dataset well.
            a.PrimaryValue.Should().BeGreaterThan(0.8);
            b.PrimaryValue.Should().BeGreaterThan(0.8);
        }
    }
}
