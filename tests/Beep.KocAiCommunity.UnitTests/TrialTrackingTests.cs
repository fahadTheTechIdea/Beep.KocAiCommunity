using System.Text;
using Beep.KocAiCommunity.Application.ML;
using Beep.KocAiCommunity.ML;
using FluentAssertions;
using Xunit;

namespace Beep.KocAiCommunity.UnitTests;

[Collection(MlTrainingCollection.Name)]
public class TrialTrackingTests
{
    private sealed class CollectingProgress : IProgress<TrialReport>
    {
        public List<TrialReport> Reports { get; } = [];
        private readonly Lock _gate = new();
        public void Report(TrialReport value)
        {
            lock (_gate)
            {
                Reports.Add(value);
            }
        }
    }

    [Fact]
    public async Task AutoML_reports_at_least_one_trial_while_training()
    {
        var sb = new StringBuilder("x1,x2,label\n");
        for (var i = 0; i < 60; i++)
        {
            sb.Append($"{7 + (i % 3)},{7 + ((i / 3) % 3)},true\n");
            sb.Append($"{i % 3},{(i / 3) % 3},false\n");
        }

        var progress = new CollectingProgress();
        var result = await new AutoMlTrainer().TrainWithTrialsAsync(
            MlTaskType.BinaryClassification, new MemoryStream(Encoding.UTF8.GetBytes(sb.ToString())), "label", 5, progress);

        result.PrimaryValue.Should().BeGreaterThan(0.8);
        progress.Reports.Should().NotBeEmpty();                                  // trials streamed live
        progress.Reports.Should().OnlyContain(r => r.MetricName == "Accuracy");
        progress.Reports.Select(r => r.TrialNumber).Should().OnlyHaveUniqueItems();
    }
}
