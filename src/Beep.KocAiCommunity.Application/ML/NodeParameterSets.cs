namespace Beep.KocAiCommunity.Application.ML;

// One parameter class per node kind. Every node is different: each declares its own strongly-typed fields.
// The shared base (NodeParameters) + the panel are the only things in common. Nodes not listed here have no
// settable parameters and use NoParameters.

// ---- Split / Model / Evaluate ----

public sealed class SplitParameters : NodeParameters
{
    public NumberParam TestFraction { get; } = new("testFraction", "Test fraction", 0.25, min: 0.05, max: 0.9, help: "Share of rows held out for evaluation.");

    protected override IReadOnlyList<ParamField> Declare() => [TestFraction];
}

// The tree-family algorithms (share trees/leaves/minLeaf).
file static class Algo
{
    public static readonly string[] Trees = ["fasttree", "fastforest", "ova-fasttree"];
    public static readonly string[] LearningRate = ["fasttree", "gam", "perceptron", "sgd", "ogd", "ova-fasttree"];
    public static readonly string[] Iterations = ["gam", "perceptron", "sgd", "ogd"];
    public static readonly string[] Linear = ["sdca", "lbfgs"];
    public static readonly string[] L2 = ["sdca", "lbfgs", "sgd", "perceptron"];
}

public sealed class TrainParameters : NodeParameters
{
    public ColumnParam TargetColumn { get; } = new("targetColumn", "Target (label) column", help: "What to predict. Fixed by the competition when submitting.");
    public ColumnParam IdColumn { get; } = new("idColumn", "ID column", help: "Row identifier carried to the submission (never used as a feature).");
    public ColumnsParam FeatureColumns { get; } = new("featureColumns", "Feature columns (blank = all except target/id)");
    public LookupParam Task { get; } = new("task", "Task", "BinaryClassification",
        [new("BinaryClassification", "Binary classification"), new("MulticlassClassification", "Multiclass classification"), new("Regression", "Regression")],
        help: "Chooses the metric and which algorithms apply.");
    public LookupParam Algorithm { get; } = new("algorithm", "Algorithm", "sdca", MlAlgorithms.All);
    public IntParam Trees { get; } = new("trees", "Trees", 100, min: 1) { VisibleWhen = new("algorithm", Algo.Trees) };
    public IntParam Leaves { get; } = new("leaves", "Leaves per tree", 20, min: 2) { VisibleWhen = new("algorithm", Algo.Trees) };
    public IntParam MinLeaf { get; } = new("minLeaf", "Min rows per leaf", 10, min: 1) { VisibleWhen = new("algorithm", Algo.Trees) };
    public NumberParam LearningRate { get; } = new("learningRate", "Learning rate", min: 0, help: "Blank = algorithm default.") { VisibleWhen = new("algorithm", Algo.LearningRate) };
    public IntParam Iterations { get; } = new("iterations", "Iterations", min: 1, help: "Blank = algorithm default.") { VisibleWhen = new("algorithm", Algo.Iterations) };
    public NumberParam L1 { get; } = new("l1", "L1 regularization", min: 0, help: "Blank = auto.") { VisibleWhen = new("algorithm", Algo.Linear) };
    public NumberParam L2 { get; } = new("l2", "L2 regularization", min: 0, help: "Blank = default.") { VisibleWhen = new("algorithm", Algo.L2) };
    public IntParam MaxIterations { get; } = new("maxIterations", "Max iterations", min: 1, help: "Blank = auto.") { VisibleWhen = new("algorithm", ["sdca"]) };
    public IntParam HistorySize { get; } = new("historySize", "History size", 20, min: 1) { VisibleWhen = new("algorithm", ["lbfgs"]) };

    protected override IReadOnlyList<ParamField> Declare() =>
        [TargetColumn, IdColumn, FeatureColumns, Task, Algorithm, Trees, Leaves, MinLeaf, LearningRate, Iterations, L1, L2, MaxIterations, HistorySize];
}

public sealed class ClusterParameters : NodeParameters
{
    public IntParam Clusters { get; } = new("clusters", "Clusters", 3, min: 2, max: 20);
    public ColumnsParam FeatureColumns { get; } = new("featureColumns", "Feature columns (blank = all numeric)");

    protected override IReadOnlyList<ParamField> Declare() => [Clusters, FeatureColumns];
}

public sealed class CrossValidateParameters : NodeParameters
{
    public IntParam Folds { get; } = new("folds", "Folds", 5, min: 2, max: 10);
    public ColumnsParam FeatureColumns { get; } = new("featureColumns", "Feature columns (blank = all except target/id)");
    public LookupParam Algorithm { get; } = new("algorithm", "Algorithm", "sdca", MlAlgorithms.All);
    public IntParam Trees { get; } = new("trees", "Trees", 100, min: 1) { VisibleWhen = new("algorithm", Algo.Trees) };
    public IntParam Leaves { get; } = new("leaves", "Leaves per tree", 20, min: 2) { VisibleWhen = new("algorithm", Algo.Trees) };
    public IntParam MinLeaf { get; } = new("minLeaf", "Min rows per leaf", 10, min: 1) { VisibleWhen = new("algorithm", Algo.Trees) };
    public NumberParam LearningRate { get; } = new("learningRate", "Learning rate", min: 0, help: "Blank = algorithm default.") { VisibleWhen = new("algorithm", Algo.LearningRate) };
    public IntParam Iterations { get; } = new("iterations", "Iterations", min: 1, help: "Blank = algorithm default.") { VisibleWhen = new("algorithm", Algo.Iterations) };
    public NumberParam L1 { get; } = new("l1", "L1 regularization", min: 0, help: "Blank = auto.") { VisibleWhen = new("algorithm", Algo.Linear) };
    public NumberParam L2 { get; } = new("l2", "L2 regularization", min: 0, help: "Blank = default.") { VisibleWhen = new("algorithm", Algo.L2) };
    public IntParam MaxIterations { get; } = new("maxIterations", "Max iterations", min: 1, help: "Blank = auto.") { VisibleWhen = new("algorithm", ["sdca"]) };
    public IntParam HistorySize { get; } = new("historySize", "History size", 20, min: 1) { VisibleWhen = new("algorithm", ["lbfgs"]) };

    protected override IReadOnlyList<ParamField> Declare() =>
        [Folds, FeatureColumns, Algorithm, Trees, Leaves, MinLeaf, LearningRate, Iterations, L1, L2, MaxIterations, HistorySize];
}

// ---- Prepare ----

public sealed class RenameColumnParameters : NodeParameters
{
    public TextParam From { get; } = new("from", "From column", required: true);
    public TextParam To { get; } = new("to", "New name", required: true);

    protected override IReadOnlyList<ParamField> Declare() => [From, To];
}

public sealed class ConvertNumericParameters : NodeParameters
{
    public ColumnsParam Columns { get; } = new("columns", "Columns (blank = all text)");

    protected override IReadOnlyList<ParamField> Declare() => [Columns];
}

public sealed class ComputeColumnParameters : NodeParameters
{
    public TextParam Output { get; } = new("output", "New column name", required: true);
    public ColumnsParam Inputs { get; } = new("inputs", "Input columns", required: true);
    public TextParam Expression { get; } = new("expression", "Formula, e.g. (gas, oil) => gas / (oil + 1)", required: true);

    protected override IReadOnlyList<ParamField> Declare() => [Output, Inputs, Expression];
}

public sealed class CombineColumnsParameters : NodeParameters
{
    public ColumnsParam Columns { get; } = new("columns", "Columns (blank = all numeric)");

    protected override IReadOnlyList<ParamField> Declare() => [Columns];
}

// ---- Shape ----

public sealed class TakeRowsParameters : NodeParameters
{
    public IntParam Count { get; } = new("count", "Rows to keep", 1000, min: 1);

    protected override IReadOnlyList<ParamField> Declare() => [Count];
}

public sealed class SampleParameters : NodeParameters
{
    public NumberParam Fraction { get; } = new("fraction", "Fraction to keep", 0.5, min: 0, max: 1);

    protected override IReadOnlyList<ParamField> Declare() => [Fraction];
}

public sealed class FilterRowsParameters : NodeParameters
{
    public ColumnParam Column { get; } = new("column", "Column", required: true);
    public NumberParam Min { get; } = new("min", "Keep ≥ min");
    public NumberParam Max { get; } = new("max", "Keep < max");

    protected override IReadOnlyList<ParamField> Declare() => [Column, Min, Max];
}

// ---- Transform ----

public sealed class SelectColumnsParameters : NodeParameters
{
    public ColumnsParam Columns { get; } = new("columns", "Columns to keep", required: true);

    protected override IReadOnlyList<ParamField> Declare() => [Columns];
}

public sealed class DropColumnsParameters : NodeParameters
{
    public ColumnsParam Columns { get; } = new("columns", "Columns to drop", required: true);

    protected override IReadOnlyList<ParamField> Declare() => [Columns];
}

public sealed class BinningParameters : NodeParameters
{
    public IntParam Bins { get; } = new("bins", "Max bins", 10, min: 2, max: 255);
    public ColumnsParam Columns { get; } = new("columns", "Columns (blank = all numeric)");

    protected override IReadOnlyList<ParamField> Declare() => [Bins, Columns];
}

public sealed class ReplaceMissingParameters : NodeParameters
{
    public LookupParam Mode { get; } = new("mode", "Replace with", "mean",
        [new("mean", "Mean"), new("min", "Minimum"), new("max", "Maximum")]);
    public ColumnsParam Columns { get; } = new("columns", "Columns (blank = all numeric)");

    protected override IReadOnlyList<ParamField> Declare() => [Mode, Columns];
}

public sealed class PcaParameters : NodeParameters
{
    public IntParam Rank { get; } = new("rank", "Components", 2, min: 1, help: "Clamped to the number of numeric features.");

    protected override IReadOnlyList<ParamField> Declare() => [Rank];
}

public sealed class FeatureSelectionParameters : NodeParameters
{
    public IntParam Count { get; } = new("count", "Min non-default count", 1, min: 1);

    protected override IReadOnlyList<ParamField> Declare() => [Count];
}

// ---- Data / DuckDB ----

public sealed class SqlParameters : NodeParameters
{
    public TextParam Sql { get; } = new("sql", "SELECT … FROM working", required: true);

    protected override IReadOnlyList<ParamField> Declare() => [Sql];
}

public sealed class SqlFilterParameters : NodeParameters
{
    public TextParam Where { get; } = new("where", "WHERE condition", required: true);

    protected override IReadOnlyList<ParamField> Declare() => [Where];
}

public sealed class GroupByParameters : NodeParameters
{
    public ColumnsParam GroupBy { get; } = new("groupBy", "Group-by columns", required: true);
    public TextParam Aggregations { get; } = new("aggregations", "Aggregates, e.g. AVG(pressure) AS avg_p", required: true);

    protected override IReadOnlyList<ParamField> Declare() => [GroupBy, Aggregations];
}

public sealed class SortParameters : NodeParameters
{
    public TextParam OrderBy { get; } = new("orderBy", "ORDER BY, e.g. pressure DESC", required: true);

    protected override IReadOnlyList<ParamField> Declare() => [OrderBy];
}

public sealed class JoinDatasetParameters : NodeParameters
{
    public DatasetParam DatasetId { get; } = new("datasetId", "Dataset to join", required: true);
    public LookupParam JoinType { get; } = new("joinType", "Join type", "left",
        [new("left", "Left (keep all current rows)"), new("inner", "Inner (only matches)"), new("right", "Right (keep all joined rows)"), new("full", "Full (keep both)")]);
    public ColumnParam On { get; } = new("on", "Key column (in both)", required: true);
    public ColumnsParam Columns { get; } = new("columns", "Columns to bring (blank = all)");

    protected override IReadOnlyList<ParamField> Declare() => [DatasetId, JoinType, On, Columns];
}

public sealed class UnionDatasetParameters : NodeParameters
{
    public DatasetParam DatasetId { get; } = new("datasetId", "Dataset to append", required: true);

    protected override IReadOnlyList<ParamField> Declare() => [DatasetId];
}

// ---- Nodes with no settable parameters ----
// Each still gets its own class (one class per node), so the panel shows "no options" and any future setting
// for that node has an obvious, named home.

/// <summary>The pipeline's data source. In free mode the user picks the training dataset here; in a
/// competition the data is fixed by the host and this is left empty (the server injects it).</summary>
public sealed class DatasetParameters : NodeParameters
{
    public DatasetParam DatasetId { get; } = new("datasetId", "Training dataset", help: "The rows this pipeline runs on.");

    protected override IReadOnlyList<ParamField> Declare() => [DatasetId];
}

public sealed class ScoreParameters : NodeParameters
{
    protected override IReadOnlyList<ParamField> Declare() => [];
}

public sealed class EvaluateParameters : NodeParameters
{
    protected override IReadOnlyList<ParamField> Declare() => [];
}

public sealed class ShuffleParameters : NodeParameters
{
    protected override IReadOnlyList<ParamField> Declare() => [];
}

public sealed class DistinctParameters : NodeParameters
{
    protected override IReadOnlyList<ParamField> Declare() => [];
}

public sealed class LpNormalizeParameters : NodeParameters
{
    protected override IReadOnlyList<ParamField> Declare() => [];
}

public sealed class GlobalContrastParameters : NodeParameters
{
    protected override IReadOnlyList<ParamField> Declare() => [];
}

// Scalers share an optional column selector (blank = all numeric feature columns).
public sealed class StandardizeParameters : NodeParameters
{
    public ColumnsParam Columns { get; } = new("columns", "Columns (blank = all numeric)");

    protected override IReadOnlyList<ParamField> Declare() => [Columns];
}

public sealed class NormalizeParameters : NodeParameters
{
    public ColumnsParam Columns { get; } = new("columns", "Columns (blank = all numeric)");

    protected override IReadOnlyList<ParamField> Declare() => [Columns];
}

public sealed class LogNormalizeParameters : NodeParameters
{
    public ColumnsParam Columns { get; } = new("columns", "Columns (blank = all numeric)");

    protected override IReadOnlyList<ParamField> Declare() => [Columns];
}

public sealed class RobustScaleParameters : NodeParameters
{
    public ColumnsParam Columns { get; } = new("columns", "Columns (blank = all numeric)");

    protected override IReadOnlyList<ParamField> Declare() => [Columns];
}

public sealed class OneHotParameters : NodeParameters
{
    public LookupParam OutputKind { get; } = new("outputKind", "Output kind", "indicator",
        [new("indicator", "Indicator (0/1 per category)"), new("bag", "Bag (counts)"), new("key", "Key (integer id)"), new("binary", "Binary encoding")]);
    public ColumnsParam Columns { get; } = new("columns", "Columns (blank = all categorical)");

    protected override IReadOnlyList<ParamField> Declare() => [OutputKind, Columns];
}

public sealed class HashEncodeParameters : NodeParameters
{
    public IntParam Bits { get; } = new("bits", "Hash bits", 16, min: 1, max: 30, help: "2^bits buckets; higher = fewer collisions.");
    public ColumnsParam Columns { get; } = new("columns", "Columns (blank = all categorical)");

    protected override IReadOnlyList<ParamField> Declare() => [Bits, Columns];
}

public sealed class FeaturizeTextParameters : NodeParameters
{
    public ColumnsParam Columns { get; } = new("columns", "Columns (blank = all text)");

    protected override IReadOnlyList<ParamField> Declare() => [Columns];
}
