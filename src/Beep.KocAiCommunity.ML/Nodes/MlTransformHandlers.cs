using Beep.KocAiCommunity.Application.ML;
using Beep.KocAiCommunity.Contracts.Workflow;
using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Transforms;
using static Beep.KocAiCommunity.ML.Nodes.NodeParam;
using static Beep.KocAiCommunity.ML.Nodes.PipelineContext;

namespace Beep.KocAiCommunity.ML.Nodes;

// One handler per transform/prepare/shape node. Bodies are ported verbatim from the monolithic
// executor's TryFeatureNode switch so behavior is identical; each mutates the shared context.

public sealed class SelectColumnsHandler : IPipelineNodeHandler
{
    public NodeEngine Engine => NodeEngine.Ml;
    public NodeDescriptor Descriptor { get; } = new("select-columns", "Transform", "Select columns",
        "Keep only the chosen feature columns; drop the rest.", PortKind.Table, PortKind.Table,
        [P("columns", "Columns to keep", NodeParameterType.Columns, required: true)]);

    public Task<NodeExecutionResult> ExecuteAsync(PipelineContext ctx, WorkflowNode node, CancellationToken ct)
    {
        var keep = SplitList(Cfg(node, "columns"));
        if (keep.Count == 0)
        {
            return Task.FromResult(new NodeExecutionResult(node.Id, node.Kind, "skipped", "no columns configured"));
        }

        var drop = ctx.FeatureColumns.Where(c => !keep.Contains(c)).ToArray();
        if (drop.Length > 0)
        {
            var t = ctx.Ml.Transforms.DropColumns(drop).Fit(ctx.TrainView);
            ctx.TrainView = t.Transform(ctx.TrainView);
            ctx.Preprocessors.Add(t);
        }

        ctx.FeatureColumns = ctx.FeatureColumns.Where(keep.Contains).ToList();
        return Task.FromResult(new NodeExecutionResult(node.Id, node.Kind, "done", $"kept {ctx.FeatureColumns.Count}: {string.Join(", ", ctx.FeatureColumns)}"));
    }
}

public sealed class DropColumnsHandler : IPipelineNodeHandler
{
    public NodeEngine Engine => NodeEngine.Ml;
    public NodeDescriptor Descriptor { get; } = new("drop-columns", "Transform", "Drop columns",
        "Remove the listed columns (ids, noise).", PortKind.Table, PortKind.Table,
        [P("columns", "Columns to drop", NodeParameterType.Columns, required: true)]);

    public Task<NodeExecutionResult> ExecuteAsync(PipelineContext ctx, WorkflowNode node, CancellationToken ct)
    {
        var drop = SplitList(Cfg(node, "columns"));
        if (drop.Count == 0)
        {
            return Task.FromResult(new NodeExecutionResult(node.Id, node.Kind, "skipped", "no columns configured"));
        }

        var toDrop = ctx.FeatureColumns.Where(drop.Contains).ToArray();
        if (toDrop.Length > 0)
        {
            var t = ctx.Ml.Transforms.DropColumns(toDrop).Fit(ctx.TrainView);
            ctx.TrainView = t.Transform(ctx.TrainView);
            ctx.Preprocessors.Add(t);
        }

        ctx.FeatureColumns = ctx.FeatureColumns.Where(c => !drop.Contains(c)).ToList();
        return Task.FromResult(new NodeExecutionResult(node.Id, node.Kind, "done", $"dropped {toDrop.Length}: {string.Join(", ", toDrop)}"));
    }
}

public sealed class SampleHandler : IPipelineNodeHandler
{
    public NodeEngine Engine => NodeEngine.Ml;
    public NodeDescriptor Descriptor { get; } = new("sample", "Transform", "Sample rows",
        "Take a fraction of the training rows.", PortKind.Table, PortKind.Table,
        [P("fraction", "Fraction to keep", NodeParameterType.Number, def: "0.5")]);

    public Task<NodeExecutionResult> ExecuteAsync(PipelineContext ctx, WorkflowNode node, CancellationToken ct)
    {
        var fraction = ReadDouble(Cfg(node, "fraction"), 0.5);
        var before = Count(ctx.TrainView);
        var take = Math.Max(1, (long)(before * fraction));
        ctx.TrainView = ctx.Ml.Data.TakeRows(ctx.Ml.Data.ShuffleRows(ctx.TrainView, seed: 1), take);
        return Task.FromResult(new NodeExecutionResult(node.Id, node.Kind, "done", $"{Count(ctx.TrainView)} of {before} rows ({fraction:0.##})"));
    }
}

public sealed class FilterRowsHandler : IPipelineNodeHandler
{
    public NodeEngine Engine => NodeEngine.Ml;
    public NodeDescriptor Descriptor { get; } = new("filter-rows", "Transform", "Filter rows",
        "Keep training rows where a column falls in a range.", PortKind.Table, PortKind.Table,
        [P("column", "Column", NodeParameterType.Text, required: true), P("min", "Keep ≥ min", NodeParameterType.Number), P("max", "Keep < max", NodeParameterType.Number)]);

    public Task<NodeExecutionResult> ExecuteAsync(PipelineContext ctx, WorkflowNode node, CancellationToken ct)
    {
        var column = Cfg(node, "column");
        if (string.IsNullOrWhiteSpace(column))
        {
            return Task.FromResult(new NodeExecutionResult(node.Id, node.Kind, "skipped", "no column configured"));
        }

        var min = ReadDouble(Cfg(node, "min"), double.NegativeInfinity);
        var max = ReadDouble(Cfg(node, "max"), double.PositiveInfinity);
        var before = Count(ctx.TrainView);
        ctx.TrainView = ctx.Ml.Data.FilterRowsByColumn(ctx.TrainView, column, lowerBound: min, upperBound: max);
        return Task.FromResult(new NodeExecutionResult(node.Id, node.Kind, "done", $"{Count(ctx.TrainView)} of {before} rows kept"));
    }
}

public sealed class StandardizeHandler : IPipelineNodeHandler
{
    public NodeEngine Engine => NodeEngine.Ml;
    public NodeDescriptor Descriptor { get; } = new("standardize", "Transform", "Standardize (z-score)",
        "Rescale numeric features to mean 0, variance 1.", PortKind.Table, PortKind.Table, []);

    public Task<NodeExecutionResult> ExecuteAsync(PipelineContext ctx, WorkflowNode node, CancellationToken ct)
    {
        var numeric = ctx.NumericFeatures();
        if (numeric.Length == 0)
        {
            return Task.FromResult(new NodeExecutionResult(node.Id, node.Kind, "skipped", "no numeric columns"));
        }

        var t = ctx.Ml.Transforms.NormalizeMeanVariance([.. numeric.Select(c => new InputOutputColumnPair(c))]).Fit(ctx.TrainView);
        ctx.TrainView = t.Transform(ctx.TrainView);
        ctx.Preprocessors.Add(t);
        return Task.FromResult(new NodeExecutionResult(node.Id, node.Kind, "done", "standardized (mean/variance)"));
    }
}

public sealed class NormalizeHandler : IPipelineNodeHandler
{
    public NodeEngine Engine => NodeEngine.Ml;
    public NodeDescriptor Descriptor { get; } = new("normalize", "Transform", "Normalize (min-max)",
        "Scale numeric features to 0–1.", PortKind.Table, PortKind.Table, []);

    public Task<NodeExecutionResult> ExecuteAsync(PipelineContext ctx, WorkflowNode node, CancellationToken ct) =>
        Task.FromResult(ctx.NumericNormalizer(node, node.Kind, (cols, data) => ctx.Ml.Transforms.NormalizeMinMax(cols).Fit(data), "min-max normalized"));
}

public sealed class LogNormalizeHandler : IPipelineNodeHandler
{
    public NodeEngine Engine => NodeEngine.Ml;
    public NodeDescriptor Descriptor { get; } = new("log-normalize", "Transform", "Log normalize",
        "Log-transform then scale — good for skewed rates.", PortKind.Table, PortKind.Table, []);

    public Task<NodeExecutionResult> ExecuteAsync(PipelineContext ctx, WorkflowNode node, CancellationToken ct) =>
        Task.FromResult(ctx.NumericNormalizer(node, node.Kind, (cols, data) => ctx.Ml.Transforms.NormalizeLogMeanVariance(cols).Fit(data), "log mean-variance"));
}

public sealed class RobustScaleHandler : IPipelineNodeHandler
{
    public NodeEngine Engine => NodeEngine.Ml;
    public NodeDescriptor Descriptor { get; } = new("robust-scale", "Transform", "Robust scale",
        "Scale by median and IQR — tolerant of outliers.", PortKind.Table, PortKind.Table, []);

    public Task<NodeExecutionResult> ExecuteAsync(PipelineContext ctx, WorkflowNode node, CancellationToken ct) =>
        Task.FromResult(ctx.NumericNormalizer(node, node.Kind, (cols, data) => ctx.Ml.Transforms.NormalizeRobustScaling(cols).Fit(data), "robust-scaled (median/IQR)"));
}

public sealed class BinningHandler : IPipelineNodeHandler
{
    public NodeEngine Engine => NodeEngine.Ml;
    public NodeDescriptor Descriptor { get; } = new("binning", "Transform", "Bin values",
        "Quantile-bin numeric features into buckets.", PortKind.Table, PortKind.Table,
        [P("bins", "Max bins", NodeParameterType.Number, def: "10")]);

    public Task<NodeExecutionResult> ExecuteAsync(PipelineContext ctx, WorkflowNode node, CancellationToken ct)
    {
        var bins = Math.Clamp((int)ReadDouble(Cfg(node, "bins"), 10), 2, 255);
        return Task.FromResult(ctx.NumericNormalizer(node, node.Kind, (cols, data) => ctx.Ml.Transforms.NormalizeBinning(cols, maximumBinCount: bins).Fit(data), $"binned into ≤{bins}"));
    }
}

public sealed class ReplaceMissingHandler : IPipelineNodeHandler
{
    public NodeEngine Engine => NodeEngine.Ml;
    public NodeDescriptor Descriptor { get; } = new("replace-missing", "Transform", "Replace missing",
        "Impute missing numeric values (common in PI sensor gaps).", PortKind.Table, PortKind.Table,
        [P("mode", "Replace with", NodeParameterType.Select, def: "mean", options: ["mean", "min", "max"])]);

    public Task<NodeExecutionResult> ExecuteAsync(PipelineContext ctx, WorkflowNode node, CancellationToken ct)
    {
        var numeric = ctx.NumericFeatures();
        if (numeric.Length == 0)
        {
            return Task.FromResult(new NodeExecutionResult(node.Id, node.Kind, "skipped", "no numeric columns"));
        }

        var mode = (Cfg(node, "mode") ?? "mean").ToLowerInvariant() switch
        {
            "min" or "minimum" => MissingValueReplacingEstimator.ReplacementMode.Minimum,
            "max" or "maximum" => MissingValueReplacingEstimator.ReplacementMode.Maximum,
            _ => MissingValueReplacingEstimator.ReplacementMode.Mean,
        };
        var t = ctx.Ml.Transforms.ReplaceMissingValues([.. numeric.Select(c => new InputOutputColumnPair(c))], replacementMode: mode).Fit(ctx.TrainView);
        ctx.TrainView = t.Transform(ctx.TrainView);
        ctx.Preprocessors.Add(t);
        return Task.FromResult(new NodeExecutionResult(node.Id, node.Kind, "done", $"missing values → {mode}"));
    }
}

public sealed class OneHotHandler : IPipelineNodeHandler
{
    public NodeEngine Engine => NodeEngine.Ml;
    public NodeDescriptor Descriptor { get; } = new("one-hot", "Transform", "One-hot encode",
        "Turn categorical columns into indicator columns.", PortKind.Table, PortKind.Table, []);

    public Task<NodeExecutionResult> ExecuteAsync(PipelineContext ctx, WorkflowNode node, CancellationToken ct)
    {
        var textCols = TextFeatures(ctx.TrainView.Schema, ctx.FeatureColumns);
        if (textCols.Length == 0)
        {
            return Task.FromResult(new NodeExecutionResult(node.Id, node.Kind, "skipped", "no categorical columns"));
        }

        var t = ctx.Ml.Transforms.Categorical.OneHotEncoding([.. textCols.Select(c => new InputOutputColumnPair(c))]).Fit(ctx.TrainView);
        ctx.TrainView = t.Transform(ctx.TrainView);
        ctx.Preprocessors.Add(t);
        return Task.FromResult(new NodeExecutionResult(node.Id, node.Kind, "done", $"encoded {textCols.Length}: {string.Join(", ", textCols)}"));
    }
}

public sealed class HashEncodeHandler : IPipelineNodeHandler
{
    public NodeEngine Engine => NodeEngine.Ml;
    public NodeDescriptor Descriptor { get; } = new("hash-encode", "Transform", "Hash encode",
        "Hash high-cardinality categoricals (e.g. well ids) into a fixed width.", PortKind.Table, PortKind.Table, []);

    public Task<NodeExecutionResult> ExecuteAsync(PipelineContext ctx, WorkflowNode node, CancellationToken ct)
    {
        var textCols = TextFeatures(ctx.TrainView.Schema, ctx.FeatureColumns);
        if (textCols.Length == 0)
        {
            return Task.FromResult(new NodeExecutionResult(node.Id, node.Kind, "skipped", "no categorical columns"));
        }

        var t = ctx.Ml.Transforms.Categorical.OneHotHashEncoding([.. textCols.Select(c => new InputOutputColumnPair(c))]).Fit(ctx.TrainView);
        ctx.TrainView = t.Transform(ctx.TrainView);
        ctx.Preprocessors.Add(t);
        return Task.FromResult(new NodeExecutionResult(node.Id, node.Kind, "done", $"hash-encoded {textCols.Length}: {string.Join(", ", textCols)}"));
    }
}

public sealed class FeaturizeTextHandler : IPipelineNodeHandler
{
    public NodeEngine Engine => NodeEngine.Ml;
    public NodeDescriptor Descriptor { get; } = new("featurize-text", "Transform", "Featurize text",
        "Turn free-text (e.g. HSE reports) into numeric vectors.", PortKind.Table, PortKind.Table, []);

    public Task<NodeExecutionResult> ExecuteAsync(PipelineContext ctx, WorkflowNode node, CancellationToken ct)
    {
        var textCols = TextFeatures(ctx.TrainView.Schema, ctx.FeatureColumns);
        if (textCols.Length == 0)
        {
            return Task.FromResult(new NodeExecutionResult(node.Id, node.Kind, "skipped", "no text columns"));
        }

        IEstimator<ITransformer>? est = null;
        foreach (var c in textCols)
        {
            var step = ctx.Ml.Transforms.Text.FeaturizeText(c, c);
            est = est is null ? step : est.Append(step);
        }

        var t = est!.Fit(ctx.TrainView);
        ctx.TrainView = t.Transform(ctx.TrainView);
        ctx.Preprocessors.Add(t);
        return Task.FromResult(new NodeExecutionResult(node.Id, node.Kind, "done", $"featurized text: {string.Join(", ", textCols)}"));
    }
}

public sealed class PcaHandler : IPipelineNodeHandler
{
    public NodeEngine Engine => NodeEngine.Ml;
    public NodeDescriptor Descriptor { get; } = new("pca", "Transform", "PCA",
        "Reduce features to N principal components.", PortKind.Table, PortKind.Table,
        [P("rank", "Components", NodeParameterType.Number, def: "2")]);

    public Task<NodeExecutionResult> ExecuteAsync(PipelineContext ctx, WorkflowNode node, CancellationToken ct)
    {
        var numeric = ctx.NumericFeatures();
        if (numeric.Length < 2)
        {
            return Task.FromResult(new NodeExecutionResult(node.Id, node.Kind, "skipped", "need ≥2 numeric columns"));
        }

        var rank = Math.Clamp((int)ReadDouble(Cfg(node, "rank"), 2), 1, numeric.Length);
        var t = ctx.Ml.Transforms.Concatenate("__PcaIn", numeric)
            .Append(ctx.Ml.Transforms.ProjectToPrincipalComponents("Pca", "__PcaIn", rank: rank))
            .Fit(ctx.TrainView);
        ctx.TrainView = t.Transform(ctx.TrainView);
        ctx.Preprocessors.Add(t);
        ctx.FeatureColumns = ["Pca"];
        return Task.FromResult(new NodeExecutionResult(node.Id, node.Kind, "done", $"PCA → {rank} components"));
    }
}

public sealed class FeatureSelectionHandler : IPipelineNodeHandler
{
    public NodeEngine Engine => NodeEngine.Ml;
    public NodeDescriptor Descriptor { get; } = new("feature-selection", "Transform", "Feature selection",
        "Drop near-constant features.", PortKind.Table, PortKind.Table,
        [P("count", "Min non-default count", NodeParameterType.Number, def: "1")]);

    public Task<NodeExecutionResult> ExecuteAsync(PipelineContext ctx, WorkflowNode node, CancellationToken ct)
    {
        var numeric = ctx.NumericFeatures();
        if (numeric.Length == 0)
        {
            return Task.FromResult(new NodeExecutionResult(node.Id, node.Kind, "skipped", "no numeric columns"));
        }

        var count = Math.Max(1, (int)ReadDouble(Cfg(node, "count"), 1));
        var t = ctx.Ml.Transforms.Concatenate("__FsIn", numeric)
            .Append(ctx.Ml.Transforms.FeatureSelection.SelectFeaturesBasedOnCount("Fs", "__FsIn", count: count))
            .Fit(ctx.TrainView);
        ctx.TrainView = t.Transform(ctx.TrainView);
        ctx.Preprocessors.Add(t);
        ctx.FeatureColumns = ["Fs"];
        return Task.FromResult(new NodeExecutionResult(node.Id, node.Kind, "done", $"selected features (count ≥ {count})"));
    }
}
