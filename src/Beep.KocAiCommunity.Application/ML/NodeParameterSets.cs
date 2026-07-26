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

public sealed class TrainParameters : NodeParameters
{
    public LookupParam Algorithm { get; } = new("algorithm", "Algorithm", "sdca", MlAlgorithms.All);
    public IntParam Trees { get; } = new("trees", "Trees", 100, min: 1, help: "FastTree / FastForest only.");
    public IntParam Leaves { get; } = new("leaves", "Leaves per tree", 20, min: 2, help: "FastTree / FastForest only.");
    public NumberParam LearningRate { get; } = new("learningRate", "Learning rate", 0.2, min: 0, help: "FastTree only.");
    public NumberParam L2 { get; } = new("l2", "L2 regularization", min: 0, help: "SDCA / L-BFGS; blank = trainer default.");

    protected override IReadOnlyList<ParamField> Declare() => [Algorithm, Trees, Leaves, LearningRate, L2];
}

public sealed class ClusterParameters : NodeParameters
{
    public IntParam Clusters { get; } = new("clusters", "Clusters", 3, min: 2, max: 20);

    protected override IReadOnlyList<ParamField> Declare() => [Clusters];
}

public sealed class CrossValidateParameters : NodeParameters
{
    public IntParam Folds { get; } = new("folds", "Folds", 5, min: 2, max: 10);
    public LookupParam Algorithm { get; } = new("algorithm", "Algorithm", "sdca", MlAlgorithms.All);
    public IntParam Trees { get; } = new("trees", "Trees", 100, min: 1, help: "FastTree / FastForest only.");
    public IntParam Leaves { get; } = new("leaves", "Leaves per tree", 20, min: 2, help: "FastTree / FastForest only.");
    public NumberParam LearningRate { get; } = new("learningRate", "Learning rate", 0.2, min: 0, help: "FastTree only.");
    public NumberParam L2 { get; } = new("l2", "L2 regularization", min: 0, help: "SDCA / L-BFGS; blank = trainer default.");

    protected override IReadOnlyList<ParamField> Declare() => [Folds, Algorithm, Trees, Leaves, LearningRate, L2];
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

    protected override IReadOnlyList<ParamField> Declare() => [Bins];
}

public sealed class ReplaceMissingParameters : NodeParameters
{
    public LookupParam Mode { get; } = new("mode", "Replace with", "mean",
        [new("mean", "Mean"), new("min", "Minimum"), new("max", "Maximum")]);

    protected override IReadOnlyList<ParamField> Declare() => [Mode];
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
    public TextParam On { get; } = new("on", "Key column (in both)", required: true);
    public ColumnsParam Columns { get; } = new("columns", "Columns to bring (blank = all)");

    protected override IReadOnlyList<ParamField> Declare() => [DatasetId, On, Columns];
}

public sealed class UnionDatasetParameters : NodeParameters
{
    public DatasetParam DatasetId { get; } = new("datasetId", "Dataset to append", required: true);

    protected override IReadOnlyList<ParamField> Declare() => [DatasetId];
}

// ---- Nodes with no settable parameters ----
// Each still gets its own class (one class per node), so the panel shows "no options" and any future setting
// for that node has an obvious, named home.

/// <summary>The pipeline's data source — its dataset is supplied by the run/competition, not a node field.</summary>
public sealed class DatasetParameters : NodeParameters
{
    protected override IReadOnlyList<ParamField> Declare() => [];
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

public sealed class StandardizeParameters : NodeParameters
{
    protected override IReadOnlyList<ParamField> Declare() => [];
}

public sealed class NormalizeParameters : NodeParameters
{
    protected override IReadOnlyList<ParamField> Declare() => [];
}

public sealed class LogNormalizeParameters : NodeParameters
{
    protected override IReadOnlyList<ParamField> Declare() => [];
}

public sealed class RobustScaleParameters : NodeParameters
{
    protected override IReadOnlyList<ParamField> Declare() => [];
}

public sealed class OneHotParameters : NodeParameters
{
    protected override IReadOnlyList<ParamField> Declare() => [];
}

public sealed class HashEncodeParameters : NodeParameters
{
    protected override IReadOnlyList<ParamField> Declare() => [];
}

public sealed class FeaturizeTextParameters : NodeParameters
{
    protected override IReadOnlyList<ParamField> Declare() => [];
}
