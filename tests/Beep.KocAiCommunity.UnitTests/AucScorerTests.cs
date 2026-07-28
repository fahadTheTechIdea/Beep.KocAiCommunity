using System.Text;
using Beep.KocAiCommunity.Infrastructure.Competitions;
using FluentAssertions;
using Xunit;

namespace Beep.KocAiCommunity.UnitTests;

public class AucScorerTests
{
    private static Stream Csv(string content) => new MemoryStream(Encoding.UTF8.GetBytes(content));

    [Fact]
    public void Higher_is_better_for_anomaly_ranking() => new AucScorer().HigherIsBetter.Should().BeTrue();

    [Fact]
    public async Task Perfect_ranking_scores_one()
    {
        // Every anomaly (label 1) scored higher than every normal (label 0) ⇒ AUC = 1.
        var preds = "id,value\nn1,0.1\nn2,0.2\na1,0.9\na2,0.8\n";
        var key = "id,label\nn1,0\nn2,0\na1,1\na2,1\n";
        (await new AucScorer().ScoreAsync(Csv(preds), Csv(key))).Should().BeApproximately(1.0, 1e-9);
    }

    [Fact]
    public async Task Reversed_ranking_scores_zero()
    {
        var preds = "id,value\nn1,0.9\nn2,0.8\na1,0.1\na2,0.2\n";
        var key = "id,label\nn1,0\nn2,0\na1,1\na2,1\n";
        (await new AucScorer().ScoreAsync(Csv(preds), Csv(key))).Should().BeApproximately(0.0, 1e-9);
    }

    [Fact]
    public async Task Aligns_by_id_not_row_order()
    {
        var preds = "id,value\na1,0.9\nn2,0.2\nn1,0.1\na2,0.8\n";
        var key = "id,label\nn1,0\nn2,0\na1,1\na2,1\n";
        (await new AucScorer().ScoreAsync(Csv(preds), Csv(key))).Should().BeApproximately(1.0, 1e-9);
    }

    [Fact]
    public async Task Missing_prediction_for_a_true_anomaly_is_penalised()
    {
        // a2 has no prediction ⇒ ranked most-normal ⇒ worse than a perfect 1.0.
        var preds = "id,value\nn1,0.1\nn2,0.2\na1,0.9\n";
        var key = "id,label\nn1,0\nn2,0\na1,1\na2,1\n";
        var score = await new AucScorer().ScoreAsync(Csv(preds), Csv(key));
        score.Should().BeLessThan(1.0).And.BeGreaterThanOrEqualTo(0.0);
    }

    [Fact]
    public async Task Empty_answer_key_is_rejected() =>
        await new AucScorer().Invoking(s => s.ScoreAsync(Csv("id,value\nn1,0.1\n"), Csv("id,label\n")))
            .Should().ThrowAsync<InvalidOperationException>();
}
