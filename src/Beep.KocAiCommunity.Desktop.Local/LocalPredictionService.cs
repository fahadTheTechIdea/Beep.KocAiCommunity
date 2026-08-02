using Beep.KocAiCommunity.Application.Common;
using Beep.KocAiCommunity.Application.ML;

namespace Beep.KocAiCommunity.Desktop.Local;

/// <summary>The outcome of scoring a file: where it was written, or which columns were missing.</summary>
public sealed record BatchPredictionResult(string? OutputPath, int RowCount, IReadOnlyList<string> MissingColumns)
{
    public bool Succeeded => OutputPath is not null;
}

/// <summary>
/// Predictions against a kept model, on this machine.
/// <para>
/// Two shapes, matching the two ways a person checks a model: one row typed in — "what would it say
/// about this well?" — and a CSV, which is the one anybody uses for real work.
/// </para>
/// </summary>
public sealed class LocalPredictionService(LocalModelStore models, IPredictionPool pool)
{
    /// <summary>Scores one row of column→value.</summary>
    public async Task<Prediction?> PredictAsync(
        LocalModelVersion model, IReadOnlyDictionary<string, string> values, CancellationToken ct = default)
    {
        var result = await pool.PredictAsync(
            model.Id, c => Load(model, c), model.TargetColumn, [values], ct);

        return result.Predictions.FirstOrDefault();
    }

    /// <summary>
    /// Scores a CSV and writes it back with a prediction column appended, beside the input.
    /// <para>
    /// A file missing a feature is the common failure, and it is answered before anything is scored:
    /// the caller gets the column names, not a framework exception several layers down.
    /// </para>
    /// </summary>
    public async Task<BatchPredictionResult> PredictFileAsync(
        LocalModelVersion model, string csvPath, CancellationToken ct = default)
    {
        // Parsed as records rather than lines: a quoted field may contain a newline, and splitting on
        // those would silently shift every column after it.
        var records = KocCsv.ParseRecords(await File.ReadAllTextAsync(csvPath, ct)).ToList();
        if (records.Count < 2)
        {
            return new BatchPredictionResult(null, 0, []);
        }

        var header = records[0];
        var missing = model.Features
            .Where(f => !header.Any(h => string.Equals(h, f.Name, StringComparison.OrdinalIgnoreCase)))
            .Select(f => f.Name)
            .ToList();

        if (missing.Count > 0)
        {
            return new BatchPredictionResult(null, 0, missing);
        }

        var data = records.Skip(1).ToList();
        var rows = data
            .Select(fields =>
            {
                var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (var i = 0; i < header.Length && i < fields.Length; i++)
                {
                    row[header[i]] = fields[i];
                }

                return (IReadOnlyDictionary<string, string>)row;
            })
            .ToList();

        if (rows.Count == 0)
        {
            return new BatchPredictionResult(null, 0, []);
        }

        var scored = await pool.PredictAsync(model.Id, c => Load(model, c), model.TargetColumn, rows, ct);

        var directory = Path.GetDirectoryName(csvPath) ?? ".";
        var outputPath = Path.Combine(
            directory, $"{Path.GetFileNameWithoutExtension(csvPath)}-predictions.csv");

        await using var writer = new StreamWriter(outputPath);
        await writer.WriteLineAsync(KocCsv.WriteRow([.. header, "prediction"]));
        for (var i = 0; i < data.Count; i++)
        {
            var prediction = i < scored.Predictions.Count ? Describe(scored.Predictions[i]) : "";
            await writer.WriteLineAsync(KocCsv.WriteRow([.. data[i], prediction]));
        }

        return new BatchPredictionResult(outputPath, rows.Count, []);
    }

    /// <summary>
    /// The one number or label a person came for. Classification answers with its label, regression and
    /// anomaly scoring with their score.
    /// </summary>
    public static string Describe(Prediction prediction) =>
        !string.IsNullOrEmpty(prediction.PredictedLabel)
            ? prediction.PredictedLabel
            : prediction.Score?.ToString("0.####") ?? "";

    /// <summary>Drops a model from the pool's cache — call after deleting or replacing it.</summary>
    public void Forget(Guid modelId) => pool.Evict(modelId);

    private async Task<byte[]> Load(LocalModelVersion model, CancellationToken ct) =>
        await models.ReadModelAsync(model.Id, ct)
        ?? throw new InvalidOperationException($"The file for “{model.Name}” v{model.Version} is missing.");
}
