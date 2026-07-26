using System.Text;
using Beep.KocAiCommunity.Infrastructure.Competitions;
using FluentAssertions;
using Xunit;

namespace Beep.KocAiCommunity.UnitTests;

public class RmseScorerTests
{
    private static Stream Csv(string content) => new MemoryStream(Encoding.UTF8.GetBytes(content));

    [Fact]
    public async Task Perfect_predictions_score_zero()
    {
        var scorer = new RmseScorer();
        var score = await scorer.ScoreAsync(Csv("id,value\n1,10\n2,20\n3,30\n"), Csv("id,oil_rate\n1,10\n2,20\n3,30\n"));
        score.Should().Be(0d);
    }

    [Fact]
    public async Task Rmse_is_root_mean_squared_error()
    {
        var scorer = new RmseScorer();
        // errors: 0, 2, 4 → squared 0,4,16 → mean 6.667 → sqrt ≈ 2.582
        var score = await scorer.ScoreAsync(Csv("id,value\n1,10\n2,22\n3,34\n"), Csv("id,oil_rate\n1,10\n2,20\n3,30\n"));
        score.Should().BeApproximately(Math.Sqrt(20d / 3d), 1e-9);
    }

    [Fact]
    public void Lower_is_better_for_regression() => new RmseScorer().HigherIsBetter.Should().BeFalse();

    [Fact]
    public async Task Aligns_by_id_not_row_order()
    {
        var score = await new RmseScorer().ScoreAsync(Csv("id,value\n3,30\n1,10\n2,20\n"), Csv("id,oil_rate\n1,10\n2,20\n3,30\n"));
        score.Should().Be(0d);
    }

    [Fact]
    public async Task Nan_prediction_is_treated_as_missing_not_poison()
    {
        // A NaN prediction must not make the whole score NaN — it's penalised like a miss.
        var score = await new RmseScorer().ScoreAsync(Csv("id,value\n1,10\n2,NaN\n"), Csv("id,oil_rate\n1,10\n2,20\n"));
        double.IsFinite(score).Should().BeTrue();
        score.Should().BeGreaterThan(0d);
    }

    [Fact]
    public async Task Header_is_detected_by_custom_id_column()
    {
        var score = await new RmseScorer().ScoreAsync(Csv("well_id,value\nW1,10\n"), Csv("well_id,oil_rate\nW1,10\n"), idColumn: "well_id");
        score.Should().Be(0d);
    }

    [Fact]
    public async Task Empty_answer_key_is_rejected_not_scored_perfect() =>
        // Parity with accuracy: a degenerate key must fail loudly, not return 0.0 (a perfect RMSE).
        await new RmseScorer().Invoking(s => s.ScoreAsync(Csv("id,value\n1,10\n"), Csv("id,oil_rate\n")))
            .Should().ThrowAsync<InvalidOperationException>();
}
