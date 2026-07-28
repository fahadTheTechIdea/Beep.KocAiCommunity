using System.Globalization;
using Beep.KocAiCommunity.Application.Competitions;

namespace Beep.KocAiCommunity.Infrastructure.Competitions;

/// <summary>
/// Root-mean-squared error for regression competitions, aligned <b>by id</b>. Lower is better. Each
/// distinct answer-key id counts once; a missing or non-numeric prediction is penalised (never treated
/// as free), and a non-finite (NaN/Infinity) prediction is treated as missing so it cannot poison the
/// aggregate.
/// </summary>
public sealed class RmseScorer : IScoringPlugin
{
    public string Code => "rmse";
    public bool HigherIsBetter => false;
    // Forecasting is scored exactly like regression (RMSE by id); it differs only in using a chronological
    // hold-out (the time-split node) rather than a random one.
    public IReadOnlyCollection<string> SupportedTasks => ["Regression", "Forecasting"];

    public async Task<double> ScoreAsync(Stream predictions, Stream answerKey, string idColumn = "id", CancellationToken ct = default)
    {
        var predRows = await CompetitionCsv.ReadAsync(predictions, idColumn, ct);
        var actual = await CompetitionCsv.ReadAsync(answerKey, idColumn, ct);

        var preds = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var (id, value) in predRows)
        {
            if (TryNumber(value, out var p))
            {
                preds[id] = p; // non-numeric / non-finite predictions fall through to the "missing" penalty
            }
        }

        double sumSquared = 0;
        var counted = 0;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (id, raw) in actual)
        {
            if (!TryNumber(raw, out var expected) || !seen.Add(id))
            {
                continue; // invalid key rows are rejected at upload; duplicates count once
            }

            counted++;
            var predicted = preds.TryGetValue(id, out var p) ? p : expected + BigMiss(expected);
            var error = predicted - expected;
            sumSquared += error * error;
        }

        if (counted == 0)
        {
            // Consistent with accuracy: a key with no scorable rows is a corrupted key, not a perfect
            // (0.0) score for everyone. It's validated non-empty at upload, so fail loudly here.
            throw new InvalidOperationException("The answer key has no scorable rows; it may be empty or malformed.");
        }

        return Math.Sqrt(sumSquared / counted);
    }

    private static bool TryNumber(string raw, out double value)
        => double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out value) && double.IsFinite(value);

    // A missing prediction should not be free — penalise it relative to the target's magnitude.
    private static double BigMiss(double expected) => Math.Max(1d, Math.Abs(expected));
}
