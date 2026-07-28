namespace Beep.KocAiCommunity.Application.Common;

/// <summary>
/// Area under the ROC curve for continuous scores against a rare binary label — the ranking metric for
/// anomaly detection, where plain accuracy is useless (99% of rows are "normal"). Shared by the Studio
/// evaluate node and the competition AUC scorer so both agree exactly.
/// </summary>
public static class RocAuc
{
    /// <summary>
    /// AUC via the rank-sum (Mann–Whitney U) identity, averaging ranks on ties. A higher score should mean
    /// "more likely positive". Returns 0.5 (no discrimination) when either class is empty.
    /// </summary>
    public static double Compute(IReadOnlyList<(double Score, bool Positive)> rows)
    {
        var n = rows.Count;
        var positives = rows.Count(r => r.Positive);
        var negatives = n - positives;
        if (positives == 0 || negatives == 0)
        {
            return 0.5;
        }

        var ordered = rows.OrderBy(r => r.Score).ToList();
        var ranks = new double[n];
        var i = 0;
        while (i < n)
        {
            var j = i;
            while (j + 1 < n && ordered[j + 1].Score.Equals(ordered[i].Score))
            {
                j++;
            }

            // 1-based average rank for the tie block [i, j].
            var averageRank = ((i + 1) + (j + 1)) / 2.0;
            for (var m = i; m <= j; m++)
            {
                ranks[m] = averageRank;
            }

            i = j + 1;
        }

        double rankSumPositive = 0;
        for (var k = 0; k < n; k++)
        {
            if (ordered[k].Positive)
            {
                rankSumPositive += ranks[k];
            }
        }

        return (rankSumPositive - ((double)positives * (positives + 1) / 2.0)) / ((double)positives * negatives);
    }
}
