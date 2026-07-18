using Beep.KocAiCommunity.Application.Competitions;

namespace Beep.KocAiCommunity.Infrastructure.Competitions;

/// <summary>
/// Fraction of answer-key rows whose predicted label matches. Inputs are CSV of <c>id,value</c>,
/// with an optional header line whose first field is "id".
/// </summary>
public sealed class AccuracyScorer : IScoringPlugin
{
    public string Code => "accuracy";
    public bool HigherIsBetter => true;

    public async Task<double> ScoreAsync(Stream predictions, Stream answerKey, CancellationToken ct = default)
    {
        var preds = await ReadCsvAsync(predictions, ct);
        var actual = await ReadCsvAsync(answerKey, ct);
        if (actual.Count == 0)
        {
            return 0d;
        }

        var correct = 0;
        foreach (var (id, expected) in actual)
        {
            if (preds.TryGetValue(id, out var predicted) && string.Equals(predicted, expected, StringComparison.OrdinalIgnoreCase))
            {
                correct++;
            }
        }

        return (double)correct / actual.Count;
    }

    private static async Task<Dictionary<string, string>> ReadCsvAsync(Stream stream, CancellationToken ct)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
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
            var value = parts[1].Trim();

            if (isFirst)
            {
                isFirst = false;
                if (id.Equals("id", StringComparison.OrdinalIgnoreCase))
                {
                    continue; // header
                }
            }

            map[id] = value;
        }

        return map;
    }
}
