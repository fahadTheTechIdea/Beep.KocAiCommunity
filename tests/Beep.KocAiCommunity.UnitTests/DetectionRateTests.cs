using Beep.KocAiCommunity.Application.Common;
using FluentAssertions;
using Xunit;

namespace Beep.KocAiCommunity.UnitTests;

/// <summary>
/// Detection rate = recall at top-K where K is the number of true anomalies — what an inspection crew
/// would actually find if it acted on the model's K loudest alarms.
/// </summary>
public class DetectionRateTests
{
    [Fact]
    public void Perfect_ranking_catches_every_anomaly()
    {
        // The two positives hold the two highest scores → both are inside the top-2.
        var rows = new[] { (9.0, true), (8.0, true), (1.0, false), (0.5, false), (0.1, false) };
        DetectionRate.Compute(rows).Should().Be(1.0);
    }

    [Fact]
    public void Half_the_anomalies_missed_gives_half_the_rate()
    {
        // Top-2 = scores 9 and 5; only one of those is a true anomaly.
        var rows = new[] { (9.0, true), (5.0, false), (4.0, true), (1.0, false) };
        DetectionRate.Compute(rows).Should().Be(0.5);
    }

    [Fact]
    public void A_flat_scorer_gets_the_base_rate_not_a_lucky_perfect_score()
    {
        // Every row scores the same, so the ranking carries no information: one tie block of 10 rows with
        // 2 positives, cut at K=2 → 2 * (2/10) = 0.4 caught of 2 = the 20% base rate.
        var rows = Enumerable.Range(0, 10).Select(i => (1.0, i < 2)).ToArray();
        DetectionRate.Compute(rows).Should().BeApproximately(0.2, 1e-9);
    }

    [Fact]
    public void Returns_zero_when_there_is_nothing_to_detect()
    {
        DetectionRate.Compute([(1.0, false), (2.0, false)]).Should().Be(0);
        DetectionRate.Compute([(1.0, true), (2.0, true)]).Should().Be(0);
        DetectionRate.Compute([]).Should().Be(0);
    }

    [Fact]
    public void Ranks_worse_than_random_score_below_the_base_rate()
    {
        // The anomalies sit at the bottom of the ranking — the top-2 cut catches neither.
        var rows = new[] { (9.0, false), (8.0, false), (2.0, true), (1.0, true) };
        DetectionRate.Compute(rows).Should().Be(0);
    }
}
