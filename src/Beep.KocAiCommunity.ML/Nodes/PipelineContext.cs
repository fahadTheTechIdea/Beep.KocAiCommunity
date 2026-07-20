using System.Globalization;
using Beep.KocAiCommunity.Application.ML;
using Beep.KocAiCommunity.Contracts.Workflow;
using Microsoft.ML;
using Microsoft.ML.Data;

namespace Beep.KocAiCommunity.ML.Nodes;

/// <summary>Which representation a node handler operates on.</summary>
public enum NodeEngine
{
    /// <summary>Reads/writes the ML.NET working view (fit/transform, models).</summary>
    Ml,

    /// <summary>Reads/writes the DuckDB working table (SQL data operations).</summary>
    Duck,
}

/// <summary>Why the pipeline is running — a preview/train-and-score, or producing predictions.</summary>
public enum PipelineMode
{
    /// <summary>Train on the split train set and evaluate on the held-out test set (Studio preview).</summary>
    Execute,

    /// <summary>Train on the full set, then predict an id,prediction CSV for the evaluation set.</summary>
    Predict,
}

/// <summary>
/// A single node kind's executor. One handler per <see cref="NodeDescriptor.Kind"/>. The handler's
/// <see cref="Descriptor"/> is the single source of truth for the catalog and the compiler; the
/// dispatcher materializes the right representation (<see cref="Engine"/>) before calling
/// <see cref="ExecuteAsync"/>.
/// </summary>
public interface IPipelineNodeHandler
{
    NodeDescriptor Descriptor { get; }
    NodeEngine Engine { get; }
    Task<NodeExecutionResult> ExecuteAsync(PipelineContext ctx, WorkflowNode node, CancellationToken ct);
}

/// <summary>
/// The mutable state threaded through a pipeline run. Holds both engine representations (the ML.NET
/// working view + the DuckDB working table) and materializes lazily across the two. ML fit/apply
/// semantics are preserved: feature handlers fit on <see cref="TrainView"/> and push an
/// <see cref="ITransformer"/> onto <see cref="Preprocessors"/>, which the evaluate/predict steps
/// re-apply to the held-out or evaluation data.
/// </summary>
public sealed class PipelineContext
{
    public required MLContext Ml { get; init; }
    public required MlTaskType Task { get; init; }
    public required PipelineMode Mode { get; init; }
    public required string LabelColumn { get; init; }
    public string? IdColumn { get; init; }

    /// <summary>Total rows loaded from the source (for the dataset node's report).</summary>
    public long SourceRowCount { get; init; }

    /// <summary>The held-out test fraction resolved from the split node (for the split node's report).</summary>
    public double SplitFraction { get; init; }

    // ---- ML working state ----
    public IDataView TrainView { get; set; } = default!;
    public IDataView? TestView { get; set; }
    public List<string> FeatureColumns { get; set; } = [];
    public List<ITransformer> Preprocessors { get; } = [];
    public ITransformer? Model { get; set; }
    public string? Algorithm { get; set; }
    public ITransformer? LabelMap { get; set; }
    public double PrimaryValue { get; set; }

    /// <summary>Per-node status collected during an Execute run.</summary>
    public List<NodeExecutionResult> Results { get; } = [];

    // ---- Reserved for the DuckDB engine (Phase 2) ----
    /// <summary>Registered dataset id → DuckDB table name for join/union nodes.</summary>
    public IReadOnlyDictionary<Guid, string> SecondaryTables { get; init; } = new Dictionary<Guid, string>();

    // ---- Helpers shared by handlers (moved verbatim from the monolithic executor) ----

    public static string? Cfg(WorkflowNode node, string key)
        => node.Config is not null && node.Config.TryGetValue(key, out var v) ? v : null;

    public static double ReadDouble(string? raw, double fallback)
        => double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : fallback;

    public static int HpInt(WorkflowNode node, string key, int fallback)
        => int.TryParse(Cfg(node, key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) && v > 0 ? v : fallback;

    public static float? HpFloat(WorkflowNode node, string key)
        => double.TryParse(Cfg(node, key), NumberStyles.Any, CultureInfo.InvariantCulture, out var v) && v > 0 ? (float)v : null;

    public static string Algo(WorkflowNode node) => (Cfg(node, "algorithm") ?? "sdca").ToLowerInvariant();

    public static HashSet<string> SplitList(string? raw)
        => (raw ?? string.Empty).Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.Ordinal);

    public static long Count(IDataView view) => view.GetRowCount() ?? view.Preview(int.MaxValue).RowView.Length;

    public string[] NumericFeatures() => NumericFeatures(TrainView.Schema, FeatureColumns);

    public static string[] NumericFeatures(DataViewSchema schema, IEnumerable<string> featureCols)
    {
        var set = featureCols.ToHashSet(StringComparer.Ordinal);
        return schema.Where(c => !c.IsHidden && set.Contains(c.Name) && IsNumeric(c.Type)).Select(c => c.Name).ToArray();
    }

    public static string[] TextFeatures(DataViewSchema schema, IEnumerable<string> featureCols)
    {
        var set = featureCols.ToHashSet(StringComparer.Ordinal);
        return schema.Where(c => !c.IsHidden && set.Contains(c.Name) && ItemType(c.Type) == TextDataViewType.Instance).Select(c => c.Name).ToArray();
    }

    public static bool IsNumeric(DataViewType type) => ItemType(type) == NumberDataViewType.Single;

    public static DataViewType ItemType(DataViewType type) => (type as VectorDataViewType)?.ItemType ?? type;

    /// <summary>Applies the accumulated preprocessors to a data view (used before scoring/evaluation).</summary>
    public IDataView ApplyPreprocessors(IDataView data)
    {
        foreach (var t in Preprocessors)
        {
            data = t.Transform(data);
        }

        return data;
    }

    /// <summary>Shared body for the per-column numeric normalizers (min-max / log / robust / binning).</summary>
    public NodeExecutionResult NumericNormalizer(WorkflowNode node, string kind, Func<InputOutputColumnPair[], IDataView, ITransformer> fit, string detail)
    {
        var numeric = NumericFeatures();
        if (numeric.Length == 0)
        {
            return new NodeExecutionResult(node.Id, kind, "skipped", "no numeric columns");
        }

        var t = fit([.. numeric.Select(c => new InputOutputColumnPair(c))], TrainView);
        TrainView = t.Transform(TrainView);
        Preprocessors.Add(t);
        return new NodeExecutionResult(node.Id, kind, "done", detail);
    }
}
