using MudBlazor;

namespace Beep.KocAiCommunity.Web.Diagrams;

public enum NodePropType { Text, Number, Columns, Select }

/// <summary>A typed, editable property of a node — drives the properties inspector.</summary>
public sealed record NodeProp(string Key, string Label, NodePropType Type, string? Default = null, string? Help = null, string[]? Options = null);

/// <summary>Metadata for one node kind — drives the palette and the properties inspector.</summary>
public sealed record NodeDef(string Kind, string Category, string Label, string Icon, string Color, string Description, NodeProp[] Props);

/// <summary>
/// The full node catalog. Each entry maps a workflow node <c>Kind</c> (understood by the executor)
/// to display metadata and a typed property schema. Adding a node = one entry here (+ executor support).
/// </summary>
public static class NodeCatalog
{
    private const string Blue = "var(--koc-accent-700)";
    private const string Teal = "#0d9488";
    private const string Amber = "#b45309";
    private const string Purple = "#6d28d9";
    private const string Green = "#15803d";
    private const string Slate = "#475569";

    public static readonly IReadOnlyList<NodeDef> All =
    [
        // ---- Data ----
        new("dataset", "Data", "Dataset", Icons.Material.Filled.TableChart, Blue,
            "The input rows flowing into the pipeline.", []),
        new("select-columns", "Data", "Select columns", Icons.Material.Filled.Checklist, Blue,
            "Keep only the chosen feature columns; drop the rest.",
            [new("columns", "Columns to keep", NodePropType.Columns, Help: "Comma-separated feature names.")]),
        new("drop-columns", "Data", "Drop columns", Icons.Material.Filled.LayersClear, Blue,
            "Remove the listed columns (e.g. ids, noise).",
            [new("columns", "Columns to drop", NodePropType.Columns)]),
        new("filter-rows", "Data", "Filter rows", Icons.Material.Filled.FilterAlt, Blue,
            "Keep training rows where a column falls in a range.",
            [
                new("column", "Column", NodePropType.Text),
                new("min", "Keep ≥ min", NodePropType.Number, Help: "Blank = no lower bound."),
                new("max", "Keep < max", NodePropType.Number, Help: "Blank = no upper bound."),
            ]),
        new("sample", "Data", "Sample rows", Icons.Material.Filled.Shuffle, Blue,
            "Take a random fraction of the training rows.",
            [new("fraction", "Fraction to keep", NodePropType.Number, "0.5")]),

        // ---- Clean ----
        new("replace-missing", "Clean", "Replace missing", Icons.Material.Filled.HealthAndSafety, Teal,
            "Impute missing numeric values.",
            [new("mode", "Replace with", NodePropType.Select, "mean", Options: ["mean", "min", "max"])]),

        // ---- Scale ----
        new("normalize", "Scale", "Normalize (min-max)", Icons.Material.Filled.Straighten, Amber,
            "Scale numeric features to the 0–1 range.", []),
        new("standardize", "Scale", "Standardize (z-score)", Icons.Material.Filled.Functions, Amber,
            "Rescale numeric features to mean 0, variance 1.", []),
        new("log-normalize", "Scale", "Log normalize", Icons.Material.Filled.ShowChart, Amber,
            "Log-transform then mean-variance scale — good for skewed values.", []),
        new("robust-scale", "Scale", "Robust scale", Icons.Material.Filled.Insights, Amber,
            "Scale by median and IQR — tolerant of outliers.", []),
        new("binning", "Scale", "Bin values", Icons.Material.Filled.BarChart, Amber,
            "Quantile-bin numeric features into buckets.",
            [new("bins", "Max bins", NodePropType.Number, "10")]),

        // ---- Reduce ----
        new("pca", "Reduce", "PCA", Icons.Material.Filled.ScatterPlot, "#0369a1",
            "Principal component analysis — reduce features to N components.",
            [new("rank", "Components", NodePropType.Number, "2")]),
        new("feature-selection", "Reduce", "Feature selection", Icons.Material.Filled.FilterList, "#0369a1",
            "Keep only features that appear often enough (drops near-constant ones).",
            [new("count", "Min non-default count", NodePropType.Number, "1")]),

        // ---- Encode ----
        new("one-hot", "Encode", "One-hot encode", Icons.Material.Filled.GridOn, Purple,
            "Turn categorical (text) columns into indicator columns.", []),
        new("hash-encode", "Encode", "Hash encode", Icons.Material.Filled.Tag, Purple,
            "Hash high-cardinality categorical columns into a fixed width.", []),
        new("featurize-text", "Encode", "Featurize text", Icons.Material.Filled.TextFields, Purple,
            "Turn free-text columns into numeric feature vectors (n-grams).", []),

        // ---- Split ----
        new("split", "Split", "Train/test split", Icons.Material.Filled.CallSplit, Slate,
            "Hold out a fraction of rows for evaluation.",
            [new("testFraction", "Test fraction", NodePropType.Number, "0.25", "Between 0.05 and 0.9.")]),

        // ---- Model ----
        new("train", "Model", "Train model", Icons.Material.Filled.ModelTraining, Green,
            "Fit a model on the training features.",
            [
                new("algorithm", "Algorithm", NodePropType.Select, "sdca"),
                new("trees", "Trees", NodePropType.Number, Help: "FastTree / FastForest only."),
                new("leaves", "Leaves", NodePropType.Number, Help: "FastTree / FastForest only."),
                new("learningRate", "Learning rate", NodePropType.Number, Help: "FastTree only."),
                new("l2", "L2 regularization", NodePropType.Number, Help: "SDCA / LBFGS only."),
            ]),
        new("cross-validate", "Model", "Cross-validate", Icons.Material.Filled.Loop, Green,
            "K-fold validation for a more honest metric.",
            [
                new("algorithm", "Algorithm", NodePropType.Select, "sdca"),
                new("folds", "Folds", NodePropType.Number, "5", "Between 2 and 10."),
            ]),
        new("cluster", "Model", "Cluster (k-means)", Icons.Material.Filled.BubbleChart, Green,
            "Unsupervised k-means clustering — groups rows, no label needed.",
            [new("clusters", "Clusters", NodePropType.Number, "3", "Between 2 and 20.")]),

        // ---- Evaluate ----
        new("score", "Evaluate", "Score", Icons.Material.Filled.Calculate, Slate,
            "Apply the trained model to the held-out set.", []),
        new("evaluate", "Evaluate", "Evaluate", Icons.Material.Filled.Assessment, Slate,
            "Compute metrics on the held-out set.", []),
    ];

    public static readonly IReadOnlyList<string> Categories =
        All.Select(n => n.Category).Distinct().ToList();

    private static readonly Dictionary<string, NodeDef> ByKind =
        All.ToDictionary(n => n.Kind, StringComparer.Ordinal);

    public static NodeDef? Get(string kind) => ByKind.GetValueOrDefault(kind);

    public static IReadOnlyList<NodeDef> InCategory(string category) =>
        All.Where(n => n.Category == category).ToList();
}
