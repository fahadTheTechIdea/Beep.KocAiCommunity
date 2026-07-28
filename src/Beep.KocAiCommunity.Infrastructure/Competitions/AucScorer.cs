using System.Globalization;
using Beep.KocAiCommunity.Application.Common;
using Beep.KocAiCommunity.Application.Competitions;

namespace Beep.KocAiCommunity.Infrastructure.Competitions;

/// <summary>
/// Area under the ROC curve for anomaly detection, aligned <b>by id</b>. The submission is a continuous
/// anomaly score per id (higher = more anomalous); the answer key is a rare binary label (1 = anomaly).
/// AUC ranks the scores against that label, which is the right metric when almost every row is normal
/// (accuracy would be ~1.0 for a model that flags nothing). Higher is better. A missing or non-numeric
/// prediction is ranked worst (most-normal), so failing to score a true anomaly is penalised.
/// </summary>
public sealed class AucScorer : IScoringPlugin
{
    public string Code => "auc";
    public bool HigherIsBetter => true;
    public IReadOnlyCollection<string> SupportedTasks => ["AnomalyDetection"];

    public async Task<double> ScoreAsync(Stream predictions, Stream answerKey, string idColumn = "id", CancellationToken ct = default)
    {
        var predRows = await CompetitionCsv.ReadAsync(predictions, idColumn, ct);
        var actual = await CompetitionCsv.ReadAsync(answerKey, idColumn, ct);

        var preds = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var (id, value) in predRows)
        {
            preds[id] = double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var s) && double.IsFinite(s)
                ? s
                : double.NegativeInfinity; // non-numeric ⇒ ranked most-normal
        }

        var rows = new List<(double Score, bool Positive)>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (id, raw) in actual)
        {
            if (!seen.Add(id))
            {
                continue; // each distinct answer-key id counts once
            }

            // A missing prediction is ranked worst (most-normal) so an un-flagged true anomaly is penalised.
            var score = preds.TryGetValue(id, out var p) ? p : double.NegativeInfinity;
            rows.Add((score, IsPositive(raw)));
        }

        if (rows.Count == 0)
        {
            throw new InvalidOperationException("The answer key has no scorable rows; it may be empty or malformed.");
        }

        return RocAuc.Compute(rows);
    }

    private static bool IsPositive(string value) => value.Trim().ToLowerInvariant() switch
    {
        "1" or "true" or "yes" or "y" or "t" => true,
        _ => false,
    };
}
