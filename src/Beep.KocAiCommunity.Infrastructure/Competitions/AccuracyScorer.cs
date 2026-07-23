using Beep.KocAiCommunity.Application.Competitions;

namespace Beep.KocAiCommunity.Infrastructure.Competitions;

/// <summary>
/// Fraction of answer-key rows whose predicted label matches, aligned <b>by id</b>. Robust to row
/// reordering, extra prediction ids, and duplicate ids (each answer-key id counts once). Common boolean
/// conventions interoperate (<c>true/1/yes</c> ≡, <c>false/0/no</c> ≡) so a competition keyed on
/// <c>1/0</c> scores a <c>true/false</c> pipeline submission correctly.
/// </summary>
public sealed class AccuracyScorer : IScoringPlugin
{
    public string Code => "accuracy";
    public bool HigherIsBetter => true;
    public IReadOnlyCollection<string> SupportedTasks => ["BinaryClassification", "MulticlassClassification"];

    public async Task<double> ScoreAsync(Stream predictions, Stream answerKey, string idColumn = "id", CancellationToken ct = default)
    {
        var predRows = await CompetitionCsv.ReadAsync(predictions, idColumn, ct);
        var actual = await CompetitionCsv.ReadAsync(answerKey, idColumn, ct);
        if (actual.Count == 0)
        {
            return 0d;
        }

        var preds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (id, value) in predRows)
        {
            preds[id] = value; // last row wins for a duplicated prediction id
        }

        var correct = 0;
        var counted = 0;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (id, expected) in actual)
        {
            if (!seen.Add(id))
            {
                continue; // count each distinct answer-key id once
            }

            counted++;
            if (preds.TryGetValue(id, out var predicted) && LabelsMatch(predicted, expected))
            {
                correct++;
            }
        }

        return counted == 0 ? 0d : (double)correct / counted;
    }

    private static bool LabelsMatch(string a, string b)
        => string.Equals(Canonical(a), Canonical(b), StringComparison.OrdinalIgnoreCase);

    // Fold the common boolean conventions together; any other token compares as itself. The \x01 sentinel
    // keeps a canonical boolean from colliding with a class literally named "true"/"false".
    private static string Canonical(string value) => value.Trim().ToLowerInvariant() switch
    {
        "true" or "1" or "yes" or "y" or "t" => "\x01true",
        "false" or "0" or "no" or "n" or "f" => "\x01false",
        _ => value.Trim(),
    };
}
