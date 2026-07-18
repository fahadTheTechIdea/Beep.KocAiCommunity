using System.Globalization;
using Beep.KocAiCommunity.Application.Competitions;

namespace Beep.KocAiCommunity.Infrastructure.Competitions;

/// <summary>
/// Root-mean-squared error for regression competitions. Inputs are CSV of <c>id,value</c> with an
/// optional header whose first field is "id". Lower is better. Only ids present in the answer key
/// count; a missing or non-numeric prediction is treated as the worst case for that row.
/// </summary>
public sealed class RmseScorer : IScoringPlugin
{
    public string Code => "rmse";
    public bool HigherIsBetter => false;

    public async Task<double> ScoreAsync(Stream predictions, Stream answerKey, CancellationToken ct = default)
    {
        var preds = await ReadCsvAsync(predictions, ct);
        var actual = await ReadCsvAsync(answerKey, ct);
        if (actual.Count == 0)
        {
            return 0d;
        }

        double sumSquared = 0;
        foreach (var (id, expected) in actual)
        {
            // A missing prediction is penalised relative to the target's magnitude, not treated as free.
            var predicted = preds.TryGetValue(id, out var p) ? p : expected + BigMiss(expected);
            var error = predicted - expected;
            sumSquared += error * error;
        }

        return Math.Sqrt(sumSquared / actual.Count);
    }

    // A missing prediction should not be free — penalise it relative to the target's magnitude.
    private static double BigMiss(double expected) => Math.Max(1d, Math.Abs(expected));

    private static async Task<Dictionary<string, double>> ReadCsvAsync(Stream stream, CancellationToken ct)
    {
        var map = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        using var reader = new StreamReader(stream, leaveOpen: true);

        var isFirst = true;
        string? line;
        while ((line = await reader.ReadLineAsync(ct)) is not null)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var parts = line.Split(',');
            if (parts.Length < 2)
            {
                continue;
            }

            var id = parts[0].Trim();
            var raw = parts[1].Trim();

            if (isFirst)
            {
                isFirst = false;
                if (id.Equals("id", StringComparison.OrdinalIgnoreCase))
                {
                    continue; // header
                }
            }

            if (double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
            {
                map[id] = value;
            }
        }

        return map;
    }
}
