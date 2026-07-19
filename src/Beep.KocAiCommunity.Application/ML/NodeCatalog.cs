namespace Beep.KocAiCommunity.Application.ML;

/// <summary>The kind of value flowing along a node port.</summary>
public enum PortKind { None, Table, Model, Metrics }

/// <summary>The editor type for a node parameter.</summary>
public enum NodeParameterType { Text, Number, Select, Columns }

/// <summary>A typed, declared parameter of a node.</summary>
public sealed record NodeParameter(
    string Name, string DisplayName, NodeParameterType Type, bool Required, string? Default, IReadOnlyList<string>? Options);

/// <summary>
/// A code-first description of one pipeline node kind: its category, ports, and typed parameters.
/// The <see cref="Kind"/> matches the workflow compiler's known kinds so a graph built from these nodes
/// validates and executes.
/// </summary>
public sealed record NodeDescriptor(
    string Kind, string Category, string DisplayName, string Description,
    PortKind Input, PortKind Output, IReadOnlyList<NodeParameter> Parameters);

/// <summary>The outcome of validating a node's parameters.</summary>
public sealed record ParameterValidation(bool IsValid, IReadOnlyList<string> Errors);

/// <summary>The backend node catalog: descriptors, lookup, and parameter validation.</summary>
public interface INodeRegistry
{
    IReadOnlyList<NodeDescriptor> All { get; }
    IReadOnlyList<string> Categories { get; }
    NodeDescriptor? Find(string kind);
    ParameterValidation ValidateParameters(string kind, IReadOnlyDictionary<string, string> config);
}

/// <summary>
/// The canonical, O&amp;G-oriented node catalog. Code-first — adding a node is one entry here (plus
/// executor support). Kept in sync with the workflow compiler's known kinds.
/// </summary>
public sealed class MlNodeRegistry : INodeRegistry
{
    private static NodeParameter P(string name, string display, NodeParameterType type, bool required = false, string? def = null, string[]? options = null)
        => new(name, display, type, required, def, options);

    private static readonly IReadOnlyList<NodeDescriptor> Nodes =
    [
        // ---- Source ----
        new("dataset", "Source", "Dataset", "The input rows flowing into the pipeline (e.g. well headers, sensor readings).",
            PortKind.None, PortKind.Table, []),

        // ---- Transform ----
        new("select-columns", "Transform", "Select columns", "Keep only the chosen feature columns; drop the rest.",
            PortKind.Table, PortKind.Table, [P("columns", "Columns to keep", NodeParameterType.Columns, required: true)]),
        new("drop-columns", "Transform", "Drop columns", "Remove the listed columns (ids, noise).",
            PortKind.Table, PortKind.Table, [P("columns", "Columns to drop", NodeParameterType.Columns, required: true)]),
        new("filter-rows", "Transform", "Filter rows", "Keep training rows where a column falls in a range.",
            PortKind.Table, PortKind.Table, [P("column", "Column", NodeParameterType.Text, required: true), P("min", "Keep ≥ min", NodeParameterType.Number), P("max", "Keep < max", NodeParameterType.Number)]),
        new("sample", "Transform", "Sample rows", "Take a fraction of the training rows.",
            PortKind.Table, PortKind.Table, [P("fraction", "Fraction to keep", NodeParameterType.Number, def: "0.5")]),
        new("replace-missing", "Transform", "Replace missing", "Impute missing numeric values (common in PI sensor gaps).",
            PortKind.Table, PortKind.Table, [P("mode", "Replace with", NodeParameterType.Select, def: "mean", options: ["mean", "min", "max"])]),
        new("normalize", "Transform", "Normalize (min-max)", "Scale numeric features to 0–1.", PortKind.Table, PortKind.Table, []),
        new("standardize", "Transform", "Standardize (z-score)", "Rescale numeric features to mean 0, variance 1.", PortKind.Table, PortKind.Table, []),
        new("log-normalize", "Transform", "Log normalize", "Log-transform then scale — good for skewed rates.", PortKind.Table, PortKind.Table, []),
        new("robust-scale", "Transform", "Robust scale", "Scale by median and IQR — tolerant of outliers.", PortKind.Table, PortKind.Table, []),
        new("binning", "Transform", "Bin values", "Quantile-bin numeric features into buckets.", PortKind.Table, PortKind.Table, [P("bins", "Max bins", NodeParameterType.Number, def: "10")]),
        new("pca", "Transform", "PCA", "Reduce features to N principal components.", PortKind.Table, PortKind.Table, [P("rank", "Components", NodeParameterType.Number, def: "2")]),
        new("feature-selection", "Transform", "Feature selection", "Drop near-constant features.", PortKind.Table, PortKind.Table, [P("count", "Min non-default count", NodeParameterType.Number, def: "1")]),
        new("one-hot", "Transform", "One-hot encode", "Turn categorical columns into indicator columns.", PortKind.Table, PortKind.Table, []),
        new("hash-encode", "Transform", "Hash encode", "Hash high-cardinality categoricals (e.g. well ids) into a fixed width.", PortKind.Table, PortKind.Table, []),
        new("featurize-text", "Transform", "Featurize text", "Turn free-text (e.g. HSE reports) into numeric vectors.", PortKind.Table, PortKind.Table, []),

        // ---- Split ----
        new("split", "Split", "Train/test split", "Hold out a fraction of rows for honest evaluation. Place before the model.",
            PortKind.Table, PortKind.Table, [P("testFraction", "Test fraction", NodeParameterType.Number, def: "0.25")]),

        // ---- Model ----
        new("train", "Model", "Train model", "Fit a model on the training features (ESP failure, production rate, …).",
            PortKind.Table, PortKind.Model, [P("algorithm", "Algorithm", NodeParameterType.Select, def: "sdca", options: ["sdca", "lbfgs", "fasttree", "fastforest"])]),
        new("cross-validate", "Model", "Cross-validate", "K-fold validation for a more honest metric.",
            PortKind.Table, PortKind.Metrics, [P("folds", "Folds", NodeParameterType.Number, def: "5")]),
        new("cluster", "Model", "Cluster (k-means)", "Unsupervised grouping — no label needed (e.g. well-log facies).",
            PortKind.Table, PortKind.Model, [P("clusters", "Clusters", NodeParameterType.Number, def: "3")]),

        // ---- Evaluate ----
        new("score", "Evaluate", "Score", "Apply the trained model to the held-out set.", PortKind.Model, PortKind.Table, []),
        new("evaluate", "Evaluate", "Evaluate", "Compute metrics on the held-out set.", PortKind.Table, PortKind.Metrics, []),
    ];

    private static readonly Dictionary<string, NodeDescriptor> ByKind = Nodes.ToDictionary(n => n.Kind, StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<NodeDescriptor> All => Nodes;
    public IReadOnlyList<string> Categories => Nodes.Select(n => n.Category).Distinct().ToList();
    public NodeDescriptor? Find(string kind) => ByKind.GetValueOrDefault(kind);

    public ParameterValidation ValidateParameters(string kind, IReadOnlyDictionary<string, string> config)
    {
        var descriptor = Find(kind);
        if (descriptor is null)
        {
            return new ParameterValidation(false, [$"Unknown node kind '{kind}'."]);
        }

        var errors = new List<string>();
        foreach (var p in descriptor.Parameters)
        {
            config.TryGetValue(p.Name, out var raw);
            var provided = !string.IsNullOrWhiteSpace(raw);

            if (p.Required && !provided)
            {
                errors.Add($"'{p.DisplayName}' is required.");
                continue;
            }

            if (!provided)
            {
                continue;
            }

            if (p.Type == NodeParameterType.Number && !double.TryParse(raw, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out _))
            {
                errors.Add($"'{p.DisplayName}' must be a number.");
            }

            if (p.Type == NodeParameterType.Select && p.Options is { Count: > 0 } && !p.Options.Contains(raw))
            {
                errors.Add($"'{p.DisplayName}' must be one of: {string.Join(", ", p.Options)}.");
            }
        }

        return new ParameterValidation(errors.Count == 0, errors);
    }
}
