using System.Globalization;
using Beep.KocAiCommunity.Application.ML;
using Beep.KocAiCommunity.Contracts.Workflow;
using Microsoft.ML;
using Microsoft.ML.Data;

namespace Beep.KocAiCommunity.ML.Nodes;

/// <summary>Why the pipeline is running — a preview/train-and-score, or producing predictions.</summary>
public enum PipelineMode
{
    Execute,
    Predict,
}

/// <summary>
/// A node's outcome: its status, the table it produced (null for terminal model/metric nodes), and an
/// optional replay step to re-apply this node's effect to the evaluation set at predict time.
/// </summary>
public sealed record NodeResult(NodeExecutionResult Status, PipelineTable? Output, ReplayStep? Replay = null);

/// <summary>
/// One node kind's executor. Every node speaks the same uniform contract: a <see cref="PipelineTable"/>
/// in, a <see cref="PipelineTable"/> out (or null for terminal nodes). Because both DuckDB and ML.NET
/// nodes materialize from and to the identical table, they are interchangeable and freely ordered.
/// </summary>
public interface IPipelineNodeHandler
{
    NodeDescriptor Descriptor { get; }
    NodeResult Execute(PipelineContext ctx, WorkflowNode node, PipelineTable input);
}

/// <summary>A fitted step recorded during training, replayed on the evaluation set at predict time.</summary>
public abstract record ReplayStep;

/// <summary>Re-runs a deterministic data node (DuckDB SQL) on the evaluation table.</summary>
public sealed record DataReplay(WorkflowNode Node, IPipelineNodeHandler Handler) : ReplayStep;

/// <summary>Applies a fitted ML.NET transformer to the evaluation table.</summary>
public sealed record TransformReplay(ITransformer Transformer) : ReplayStep;

/// <summary>
/// The mutable state threaded through a run. Nodes read/write <see cref="PipelineTable"/>s; the split
/// tags rows with a <see cref="FoldColumn"/>, so ML transforms fit on the train fold and evaluate uses
/// the test fold — the fit/apply lifecycle, preserved uniformly.
/// </summary>
public sealed class PipelineContext : IDisposable
{
    public required MLContext Ml { get; init; }
    public required MlTaskType Task { get; init; }
    public required PipelineMode Mode { get; init; }
    public required string LabelColumn { get; init; }
    public string? IdColumn { get; init; }

    /// <summary>Raw CSV bytes of the datasets a join/union node may reference (by dataset id).</summary>
    public IReadOnlyDictionary<Guid, byte[]> SecondaryDatasets { get; init; } = new Dictionary<Guid, byte[]>();

    /// <summary>The column the split node tags rows with: 0 = train, 1 = test.</summary>
    public const string FoldColumn = "__fold";

    /// <summary>The DuckDB table name that Data nodes read from and replace.</summary>
    public const string WorkingTable = "working";

    private DuckDbSession? _duck;

    // The id column (when known, i.e. at predict time) is pinned to text in DuckDB so a numeric-looking
    // id survives the SQL crossing intact and stays joinable to the answer key.
    public DuckDbSession Duck => _duck ??= new DuckDbSession(IdColumn is null ? null : [IdColumn]);

    public ITransformer? Model { get; set; }
    public string? Algorithm { get; set; }
    public ITransformer? LabelMap { get; set; }
    public double PrimaryValue { get; set; }

    /// <summary>Set once a 'split' node has run, so the fold views can detect a dropped marker (leakage).</summary>
    public bool HasSplit { get; set; }

    public List<NodeExecutionResult> Results { get; } = [];
    public List<ReplayStep> Steps { get; } = [];
    public List<string> TempFiles { get; } = [];

    private readonly Dictionary<Guid, string> _secondaryTables = [];

    /// <summary>Loads a referenced dataset into DuckDB once and returns its table name.</summary>
    public string? SecondaryTable(Guid id)
    {
        if (_secondaryTables.TryGetValue(id, out var existing))
        {
            return existing;
        }

        if (!SecondaryDatasets.TryGetValue(id, out var bytes))
        {
            return null;
        }

        var tempCsv = NewTemp();
        File.WriteAllBytes(tempCsv, bytes);
        var table = $"ds_{_secondaryTables.Count}";
        Duck.LoadCsv(tempCsv, table);
        _secondaryTables[id] = table;
        return table;
    }

    public string NewTemp()
    {
        var path = PipelineTemp.New();
        TempFiles.Add(path);
        return path;
    }

    /// <summary>
    /// The column roles for a table — the first-class X (features) / y (label) / id / fold split, resolved
    /// from this run's label and id and the table's columns. The single source of truth for "what is a
    /// feature": every node that needs the feature set goes through here.
    /// </summary>
    public ColumnRoles Roles(PipelineTable table) => ColumnRoles.Resolve(LabelColumn, IdColumn, table.Columns);

    /// <summary>Candidate feature column names in a table (everything but label, id, and the fold marker).</summary>
    public IEnumerable<string> FeatureNames(PipelineTable table) => Roles(table).Features;

    // ---- Shared helpers (moved from the monolithic executor) ----

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

    // ---- Fit/apply lifecycle over the fold marker ----

    /// <summary>The train rows of a view (fold 0) — or all rows when the table isn't split yet.</summary>
    public IDataView FoldTrainView(IDataView full)
        => HasFold(full)
            ? Ml.Data.FilterRowsByColumn(full, FoldColumn, lowerBound: double.NegativeInfinity, upperBound: 0.5)
            : full;

    /// <summary>The held-out test rows of a view (fold 1) — or all rows when the table isn't split.</summary>
    public IDataView FoldTestView(IDataView full)
        => HasFold(full)
            ? Ml.Data.FilterRowsByColumn(full, FoldColumn, lowerBound: 0.5, upperBound: double.PositiveInfinity)
            : full;

    /// <summary>
    /// True when the fold marker is present. If a split ran but the marker is gone, a downstream node
    /// dropped it — training and evaluation would then overlap (data leakage), so fail loudly instead.
    /// </summary>
    private bool HasFold(IDataView full)
    {
        var present = full.Schema.GetColumnOrNull(FoldColumn) is not null;
        if (!present && HasSplit)
        {
            throw new InvalidOperationException(
                "The train/test split marker was dropped before training. A node after 'split' "
                + "(e.g. select-columns, drop-columns, group-by, or a SQL node) removed the internal fold "
                + "column, which would make training and evaluation overlap. Keep 'split' immediately "
                + "before train/evaluate, or preserve all columns downstream of it.");
        }

        return present;
    }

    /// <summary>
    /// The standard ML transform lifecycle: build an estimator from the table's schema, fit it on the
    /// train fold, apply to every row, and record the fitted transformer for the predict replay.
    /// </summary>
    public NodeResult FitTransform(WorkflowNode node, PipelineTable input,
        Func<IDataView, (IEstimator<ITransformer>? Estimator, string Detail)> build, string skipReason)
    {
        var full = input.LoadIntoMl(Ml, LabelColumn);
        var (estimator, detail) = build(full);
        if (estimator is null)
        {
            return new NodeResult(new NodeExecutionResult(node.Id, node.Kind, "skipped", skipReason), input);
        }

        var transformer = estimator.Fit(FoldTrainView(full));
        var transformed = transformer.Transform(full);
        return new NodeResult(
            new NodeExecutionResult(node.Id, node.Kind, "done", detail),
            PipelineTable.FromMlView(transformed, TempFiles),
            new TransformReplay(transformer));
    }

    public void Dispose()
    {
        try { _duck?.Dispose(); } catch { /* releasing the in-memory DuckDB is best effort */ }
        foreach (var f in TempFiles)
        {
            PipelineTemp.Delete(f);
        }
    }
}
