using System.Text;
using Beep.KocAiCommunity.Infrastructure.Competitions;
using FluentAssertions;
using Xunit;

namespace Beep.KocAiCommunity.UnitTests;

public class AccuracyScorerTests
{
    private static Stream Csv(string content) => new MemoryStream(Encoding.UTF8.GetBytes(content));
    private static readonly AccuracyScorer Scorer = new();

    [Fact]
    public async Task Perfect_predictions_score_one()
    {
        var score = await Scorer.ScoreAsync(Csv("id,prediction\n1,a\n2,b\n"), Csv("id,label\n1,a\n2,b\n"));
        score.Should().Be(1d);
    }

    [Fact]
    public async Task Aligns_by_id_not_row_order()
    {
        // Predictions in reverse order still match by id.
        var score = await Scorer.ScoreAsync(Csv("id,prediction\n2,b\n1,a\n"), Csv("id,label\n1,a\n2,b\n"));
        score.Should().Be(1d);
    }

    [Fact]
    public async Task Boolean_conventions_interoperate_true_false_vs_1_0()
    {
        // The Titanic latent bug: pipeline emits true/false, answer key uses 1/0 → must still score.
        var score = await Scorer.ScoreAsync(Csv("id,prediction\n1,true\n2,false\n"), Csv("id,label\n1,1\n2,0\n"));
        score.Should().Be(1d);
    }

    [Fact]
    public async Task Extra_prediction_ids_are_ignored_and_missing_counts_wrong()
    {
        // id 3 missing from predictions (wrong); id 9 extra (ignored). 1 of 2 correct.
        var score = await Scorer.ScoreAsync(Csv("id,prediction\n1,a\n9,z\n"), Csv("id,label\n1,a\n3,b\n"));
        score.Should().Be(0.5d);
    }

    [Fact]
    public async Task Duplicate_answer_key_id_counts_once()
    {
        var score = await Scorer.ScoreAsync(Csv("id,prediction\n1,a\n2,b\n"), Csv("id,label\n1,a\n1,a\n2,b\n"));
        score.Should().Be(1d); // two distinct ids, both correct
    }

    [Fact]
    public async Task Multiclass_tokens_score_exactly()
    {
        var score = await Scorer.ScoreAsync(Csv("id,prediction\n1,cat\n2,dog\n3,cat\n"), Csv("id,label\n1,cat\n2,fish\n3,cat\n"));
        score.Should().BeApproximately(2d / 3d, 1e-9);
    }

    [Fact]
    public async Task Header_is_detected_by_custom_id_column()
    {
        var score = await Scorer.ScoreAsync(Csv("well_id,prediction\nW1,a\n"), Csv("well_id,label\nW1,a\n"), idColumn: "well_id");
        score.Should().Be(1d); // header rows skipped, not scored as data
    }

    [Fact]
    public async Task Empty_answer_key_is_rejected_not_silently_scored() =>
        await Scorer.Invoking(s => s.ScoreAsync(Csv("id,prediction\n1,a\n"), Csv("id,label\n")))
            .Should().ThrowAsync<InvalidOperationException>();

    [Fact]
    public async Task Boolean_folding_does_not_collide_coded_multiclass_classes()
    {
        // A 3-class key whose classes include boolean-like tokens (1 / yes / no). Folding is OFF (not a
        // binary key), so "1" and "yes" stay distinct — a "1" prediction must NOT match a "yes" label.
        var score = await Scorer.ScoreAsync(
            Csv("id,prediction\n1,1\n2,1\n3,no\n"),
            Csv("id,label\n1,1\n2,yes\n3,no\n"));
        score.Should().BeApproximately(2d / 3d, 1e-9); // row 2: predicted "1" vs label "yes" → wrong
    }
}
