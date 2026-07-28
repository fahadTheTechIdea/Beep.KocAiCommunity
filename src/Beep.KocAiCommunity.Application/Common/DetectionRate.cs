namespace Beep.KocAiCommunity.Application.Common;

/// <summary>
/// The share of true anomalies caught when you act on a realistic number of alarms — the operator-facing
/// companion to <see cref="RocAuc"/>. AUC says how well the scores rank; this says what an inspection crew
/// would actually find. It takes the top-K highest-scoring rows, where K = the number of true anomalies in
/// the set, and reports how many of them are real. At that cut precision and recall coincide, so there is
/// no threshold to argue about and the number is comparable between models.
/// </summary>
public static class DetectionRate
{
    /// <summary>
    /// Detection rate (recall at top-K, K = the positive count). Ties at the K-th score are shared
    /// proportionally, so a detector that scores every row identically gets the base rate, not a lucky 1.0.
    /// Returns 0 when either class is empty (nothing to detect, or nothing to rank against).
    /// </summary>
    public static double Compute(IReadOnlyList<(double Score, bool Positive)> rows)
    {
        var positives = rows.Count(r => r.Positive);
        if (positives == 0 || positives == rows.Count)
        {
            return 0;
        }

        // Walk from the most anomalous down, taking whole tie blocks. A block that straddles the K-th place
        // contributes its positives pro rata — the ranking gives us no reason to prefer any row inside it.
        var ordered = rows.OrderByDescending(r => r.Score).ToList();
        double caught = 0;
        var taken = 0;
        var i = 0;
        while (i < ordered.Count && taken < positives)
        {
            var j = i;
            while (j + 1 < ordered.Count && ordered[j + 1].Score.Equals(ordered[i].Score))
            {
                j++;
            }

            var blockSize = j - i + 1;
            var blockPositives = 0;
            for (var m = i; m <= j; m++)
            {
                if (ordered[m].Positive)
                {
                    blockPositives++;
                }
            }

            var room = positives - taken;
            caught += room >= blockSize ? blockPositives : blockPositives * ((double)room / blockSize);
            taken += blockSize;
            i = j + 1;
        }

        return caught / positives;
    }
}
