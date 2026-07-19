using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using Beep.KocAiCommunity.Application.ML;
using Microsoft.ML;
using Microsoft.ML.Data;

namespace Beep.KocAiCommunity.ML;

/// <summary>
/// Thread-safe, hot-reloadable pool of loaded ML.NET models. Platform models are trained on
/// arbitrary user CSV schemas, so the compile-time-typed <c>PredictionEnginePool&lt;TSrc,TDst&gt;</c>
/// does not fit; instead each model's loaded <see cref="ITransformer"/> and input schema are cached
/// and rows are scored dynamically against that schema. Registered as a singleton by the hosts.
/// </summary>
public sealed class AutoMlPredictionPool : IPredictionPool
{
    private readonly MLContext _ml = new(seed: 1);
    private readonly ConcurrentDictionary<Guid, Lazy<Task<CachedModel>>> _cache = new();

    private sealed record CachedModel(ITransformer Model, DataViewSchema InputSchema);

    public async Task<InferenceResult> PredictAsync(
        Guid modelVersionId,
        Func<CancellationToken, Task<byte[]>> modelLoader,
        string labelColumn,
        IReadOnlyList<IReadOnlyDictionary<string, string>> rows,
        CancellationToken ct = default)
    {
        var cached = await GetOrLoadAsync(modelVersionId, modelLoader, ct);
        // Scoring is CPU-bound; keep it off the request thread.
        return await Task.Run(() => Score(cached, labelColumn, rows), ct);
    }

    public void Evict(Guid modelVersionId) => _cache.TryRemove(modelVersionId, out _);

    private Task<CachedModel> GetOrLoadAsync(Guid id, Func<CancellationToken, Task<byte[]>> loader, CancellationToken ct)
    {
        var lazy = _cache.GetOrAdd(id, _ => new Lazy<Task<CachedModel>>(async () =>
        {
            var bytes = await loader(ct);
            using var ms = new MemoryStream(bytes);
            var model = _ml.Model.Load(ms, out var schema);
            return new CachedModel(model, schema);
        }));

        try
        {
            return lazy.Value;
        }
        catch
        {
            // A failed load must not poison the cache.
            _cache.TryRemove(id, out _);
            throw;
        }
    }

    private InferenceResult Score(CachedModel cached, string labelColumn, IReadOnlyList<IReadOnlyDictionary<string, string>> rows)
    {
        var columns = cached.InputSchema.Where(c => !c.IsHidden).ToList();

        // Materialize the rows as an in-memory CSV that matches the model's input schema, then load
        // it with a schema-derived TextLoader — the schema-free way to build an IDataView.
        var tempPath = Path.Combine(Path.GetTempPath(), $"koc-infer-{Guid.NewGuid():N}.csv");
        try
        {
            WriteCsv(tempPath, columns, labelColumn, rows);

            var loaderColumns = new TextLoader.Column[columns.Count];
            for (var i = 0; i < columns.Count; i++)
            {
                loaderColumns[i] = new TextLoader.Column(columns[i].Name, ToDataKind(columns[i].Type), i);
            }

            var loader = _ml.Data.CreateTextLoader(loaderColumns, hasHeader: true, separatorChar: ',');
            var scored = cached.Model.Transform(loader.Load(tempPath));
            return Extract(scored, rows.Count);
        }
        finally
        {
            try { File.Delete(tempPath); } catch (IOException) { /* best effort */ }
        }
    }

    private static void WriteCsv(string path, List<DataViewSchema.Column> columns, string labelColumn, IReadOnlyList<IReadOnlyDictionary<string, string>> rows)
    {
        var sb = new StringBuilder();
        sb.AppendJoin(',', columns.Select(c => c.Name)).Append('\n');
        foreach (var row in rows)
        {
            for (var i = 0; i < columns.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(',');
                }

                var name = columns[i].Name;
                if (string.Equals(name, labelColumn, StringComparison.OrdinalIgnoreCase))
                {
                    sb.Append(Placeholder(columns[i].Type)); // label is unknown at inference time
                }
                else if (row.TryGetValue(name, out var value) && !string.IsNullOrEmpty(value))
                {
                    sb.Append(Sanitize(value));
                }
                else
                {
                    sb.Append(Placeholder(columns[i].Type));
                }
            }

            sb.Append('\n');
        }

        File.WriteAllText(path, sb.ToString());
    }

    // Values are user-supplied; keep the CSV single-column-safe by stripping separators/newlines.
    private static string Sanitize(string value) =>
        value.Replace(',', ' ').Replace('\n', ' ').Replace('\r', ' ');

    private static string Placeholder(DataViewType type) =>
        type.RawType == typeof(ReadOnlyMemory<char>) || type.RawType == typeof(string) ? string.Empty : "0";

    private static DataKind ToDataKind(DataViewType type)
    {
        var raw = type.RawType;
        if (raw == typeof(float)) { return DataKind.Single; }
        if (raw == typeof(double)) { return DataKind.Double; }
        if (raw == typeof(bool)) { return DataKind.Boolean; }
        if (raw == typeof(int)) { return DataKind.Int32; }
        if (raw == typeof(long)) { return DataKind.Int64; }
        return DataKind.String;
    }

    private static InferenceResult Extract(IDataView scored, int expected)
    {
        var preview = scored.Preview(maxRows: Math.Max(expected, 1));
        var predictions = new List<Prediction>(preview.RowView.Length);
        foreach (var row in preview.RowView)
        {
            string? label = null;
            double? probability = null;
            double? score = null;
            IReadOnlyList<double>? scores = null;

            foreach (var (key, value) in row.Values.Select(v => (v.Key, v.Value)))
            {
                switch (key)
                {
                    case "PredictedLabel":
                        label = Format(value);
                        break;
                    case "Probability" when value is float p:
                        probability = p;
                        break;
                    case "Score":
                        if (value is VBuffer<float> vb)
                        {
                            scores = vb.DenseValues().Select(f => (double)f).ToList();
                        }
                        else if (value is float s)
                        {
                            score = s;
                        }

                        break;
                }
            }

            predictions.Add(new Prediction(label, probability, score, scores));
        }

        return new InferenceResult(predictions);
    }

    private static string Format(object? value) => value switch
    {
        null => string.Empty,
        ReadOnlyMemory<char> rom => rom.ToString(),
        bool b => b ? "true" : "false",
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty,
    };
}
