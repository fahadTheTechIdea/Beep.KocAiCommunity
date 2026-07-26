using Beep.KocAiCommunity.Application.ML;
using Beep.KocAiCommunity.Contracts.Workflow;
using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Transforms;
using static Beep.KocAiCommunity.ML.Nodes.PipelineContext;

namespace Beep.KocAiCommunity.ML.Nodes;

// ML.NET transform handlers (uniform contract: PipelineTable in → PipelineTable out). Each fits its
// transformer on the train fold via ctx.FitTransform and applies it to the whole table.

public sealed class SelectColumnsHandler : IPipelineNodeHandler
{
    public NodeDescriptor Descriptor { get; } = new("select-columns", "Transform", "Select columns",
        "Keep only the chosen feature columns; drop the rest.", PortKind.Table, PortKind.Table,
        new SelectColumnsParameters().Describe());

    public NodeResult Execute(PipelineContext ctx, WorkflowNode node, PipelineTable input)
    {
        var keep = SplitList(Cfg(node, "columns"));
        if (keep.Count == 0)
        {
            return new NodeResult(new NodeExecutionResult(node.Id, node.Kind, "skipped", "no columns configured"), input);
        }

        var drop = ctx.FeatureNames(input).Where(c => !keep.Contains(c)).ToArray();
        if (drop.Length == 0)
        {
            return new NodeResult(new NodeExecutionResult(node.Id, node.Kind, "done", $"kept {keep.Count}"), input);
        }

        return ctx.FitTransform(node, input,
            _ => (ctx.Ml.Transforms.DropColumns(drop), $"kept {keep.Count}: {string.Join(", ", ctx.FeatureNames(input).Where(keep.Contains))}"),
            "no columns configured");
    }
}

public sealed class DropColumnsHandler : IPipelineNodeHandler
{
    public NodeDescriptor Descriptor { get; } = new("drop-columns", "Transform", "Drop columns",
        "Remove the listed columns (ids, noise).", PortKind.Table, PortKind.Table,
        new DropColumnsParameters().Describe());

    public NodeResult Execute(PipelineContext ctx, WorkflowNode node, PipelineTable input)
    {
        var toDrop = SplitList(Cfg(node, "columns")).Where(input.HasColumn).ToArray();
        if (toDrop.Length == 0)
        {
            return new NodeResult(new NodeExecutionResult(node.Id, node.Kind, "skipped", "no columns configured"), input);
        }

        return ctx.FitTransform(node, input,
            _ => (ctx.Ml.Transforms.DropColumns(toDrop), $"dropped {toDrop.Length}: {string.Join(", ", toDrop)}"),
            "no columns configured");
    }
}

public sealed class StandardizeHandler : IPipelineNodeHandler
{
    public NodeDescriptor Descriptor { get; } = new("standardize", "Transform", "Standardize (z-score)",
        "Rescale numeric features to mean 0, variance 1.", PortKind.Table, PortKind.Table, new StandardizeParameters().Describe());

    public NodeResult Execute(PipelineContext ctx, WorkflowNode node, PipelineTable input) =>
        ctx.FitTransform(node, input, full =>
        {
            var numeric = NumericFeatures(full.Schema, ctx.FeatureNames(input));
            return numeric.Length == 0 ? (null, "") : (ctx.Ml.Transforms.NormalizeMeanVariance([.. numeric.Select(c => new InputOutputColumnPair(c))]), "standardized (mean/variance)");
        }, "no numeric columns");
}

public sealed class NormalizeHandler : IPipelineNodeHandler
{
    public NodeDescriptor Descriptor { get; } = new("normalize", "Transform", "Normalize (min-max)",
        "Scale numeric features to 0–1.", PortKind.Table, PortKind.Table, new NormalizeParameters().Describe());

    public NodeResult Execute(PipelineContext ctx, WorkflowNode node, PipelineTable input) =>
        Scale(ctx, node, input, (cols) => ctx.Ml.Transforms.NormalizeMinMax(cols), "min-max normalized");

    internal static NodeResult Scale(PipelineContext ctx, WorkflowNode node, PipelineTable input,
        Func<InputOutputColumnPair[], IEstimator<ITransformer>> build, string detail) =>
        ctx.FitTransform(node, input, full =>
        {
            var numeric = NumericFeatures(full.Schema, ctx.FeatureNames(input));
            return numeric.Length == 0 ? (null, "") : (build([.. numeric.Select(c => new InputOutputColumnPair(c))]), detail);
        }, "no numeric columns");
}

public sealed class LogNormalizeHandler : IPipelineNodeHandler
{
    public NodeDescriptor Descriptor { get; } = new("log-normalize", "Transform", "Log normalize",
        "Log-transform then scale — good for skewed rates.", PortKind.Table, PortKind.Table, new LogNormalizeParameters().Describe());

    public NodeResult Execute(PipelineContext ctx, WorkflowNode node, PipelineTable input) =>
        NormalizeHandler.Scale(ctx, node, input, cols => ctx.Ml.Transforms.NormalizeLogMeanVariance(cols), "log mean-variance");
}

public sealed class RobustScaleHandler : IPipelineNodeHandler
{
    public NodeDescriptor Descriptor { get; } = new("robust-scale", "Transform", "Robust scale",
        "Scale by median and IQR — tolerant of outliers.", PortKind.Table, PortKind.Table, new RobustScaleParameters().Describe());

    public NodeResult Execute(PipelineContext ctx, WorkflowNode node, PipelineTable input) =>
        NormalizeHandler.Scale(ctx, node, input, cols => ctx.Ml.Transforms.NormalizeRobustScaling(cols), "robust-scaled (median/IQR)");
}

public sealed class BinningHandler : IPipelineNodeHandler
{
    public NodeDescriptor Descriptor { get; } = new("binning", "Transform", "Bin values",
        "Quantile-bin numeric features into buckets.", PortKind.Table, PortKind.Table,
        new BinningParameters().Describe());

    public NodeResult Execute(PipelineContext ctx, WorkflowNode node, PipelineTable input)
    {
        var bins = Math.Clamp((int)ReadDouble(Cfg(node, "bins"), 10), 2, 255);
        return NormalizeHandler.Scale(ctx, node, input, cols => ctx.Ml.Transforms.NormalizeBinning(cols, maximumBinCount: bins), $"binned into ≤{bins}");
    }
}

public sealed class ReplaceMissingHandler : IPipelineNodeHandler
{
    public NodeDescriptor Descriptor { get; } = new("replace-missing", "Transform", "Replace missing",
        "Impute missing numeric values (common in PI sensor gaps).", PortKind.Table, PortKind.Table,
        new ReplaceMissingParameters().Describe());

    public NodeResult Execute(PipelineContext ctx, WorkflowNode node, PipelineTable input) =>
        ctx.FitTransform(node, input, full =>
        {
            var numeric = NumericFeatures(full.Schema, ctx.FeatureNames(input));
            if (numeric.Length == 0)
            {
                return (null, "");
            }

            var mode = (Cfg(node, "mode") ?? "mean").ToLowerInvariant() switch
            {
                "min" or "minimum" => MissingValueReplacingEstimator.ReplacementMode.Minimum,
                "max" or "maximum" => MissingValueReplacingEstimator.ReplacementMode.Maximum,
                _ => MissingValueReplacingEstimator.ReplacementMode.Mean,
            };
            return (ctx.Ml.Transforms.ReplaceMissingValues([.. numeric.Select(c => new InputOutputColumnPair(c))], replacementMode: mode), $"missing values → {mode}");
        }, "no numeric columns");
}

public sealed class OneHotHandler : IPipelineNodeHandler
{
    public NodeDescriptor Descriptor { get; } = new("one-hot", "Transform", "One-hot encode",
        "Turn categorical columns into indicator columns.", PortKind.Table, PortKind.Table, new OneHotParameters().Describe());

    public NodeResult Execute(PipelineContext ctx, WorkflowNode node, PipelineTable input) =>
        ctx.FitTransform(node, input, full =>
        {
            var textCols = TextFeatures(full.Schema, ctx.FeatureNames(input));
            return textCols.Length == 0 ? (null, "") : (ctx.Ml.Transforms.Categorical.OneHotEncoding([.. textCols.Select(c => new InputOutputColumnPair(c))]), $"encoded {textCols.Length}: {string.Join(", ", textCols)}");
        }, "no categorical columns");
}

public sealed class HashEncodeHandler : IPipelineNodeHandler
{
    public NodeDescriptor Descriptor { get; } = new("hash-encode", "Transform", "Hash encode",
        "Hash high-cardinality categoricals (e.g. well ids) into a fixed width.", PortKind.Table, PortKind.Table, new HashEncodeParameters().Describe());

    public NodeResult Execute(PipelineContext ctx, WorkflowNode node, PipelineTable input) =>
        ctx.FitTransform(node, input, full =>
        {
            var textCols = TextFeatures(full.Schema, ctx.FeatureNames(input));
            return textCols.Length == 0 ? (null, "") : (ctx.Ml.Transforms.Categorical.OneHotHashEncoding([.. textCols.Select(c => new InputOutputColumnPair(c))]), $"hash-encoded {textCols.Length}: {string.Join(", ", textCols)}");
        }, "no categorical columns");
}

public sealed class FeaturizeTextHandler : IPipelineNodeHandler
{
    public NodeDescriptor Descriptor { get; } = new("featurize-text", "Transform", "Featurize text",
        "Turn free-text (e.g. HSE reports) into numeric vectors.", PortKind.Table, PortKind.Table, new FeaturizeTextParameters().Describe());

    public NodeResult Execute(PipelineContext ctx, WorkflowNode node, PipelineTable input) =>
        ctx.FitTransform(node, input, full =>
        {
            var textCols = TextFeatures(full.Schema, ctx.FeatureNames(input));
            if (textCols.Length == 0)
            {
                return (null, "");
            }

            IEstimator<ITransformer>? est = null;
            foreach (var c in textCols)
            {
                var step = ctx.Ml.Transforms.Text.FeaturizeText(c, c);
                est = est is null ? step : est.Append(step);
            }

            return (est, $"featurized text: {string.Join(", ", textCols)}");
        }, "no text columns");
}

public sealed class PcaHandler : IPipelineNodeHandler
{
    public NodeDescriptor Descriptor { get; } = new("pca", "Transform", "PCA",
        "Reduce features to N principal components.", PortKind.Table, PortKind.Table,
        new PcaParameters().Describe());

    public NodeResult Execute(PipelineContext ctx, WorkflowNode node, PipelineTable input) =>
        ctx.FitTransform(node, input, full =>
        {
            var numeric = NumericFeatures(full.Schema, ctx.FeatureNames(input));
            if (numeric.Length < 2)
            {
                return (null, "");
            }

            var rank = Math.Clamp((int)ReadDouble(Cfg(node, "rank"), 2), 1, numeric.Length);
            var est = ctx.Ml.Transforms.Concatenate("__PcaIn", numeric)
                .Append(ctx.Ml.Transforms.ProjectToPrincipalComponents("Pca", "__PcaIn", rank: rank))
                .Append(ctx.Ml.Transforms.DropColumns([.. numeric, "__PcaIn"]));
            return (est, $"PCA → {rank} components");
        }, "need ≥2 numeric columns");
}

public sealed class FeatureSelectionHandler : IPipelineNodeHandler
{
    public NodeDescriptor Descriptor { get; } = new("feature-selection", "Transform", "Feature selection",
        "Drop near-constant features.", PortKind.Table, PortKind.Table,
        new FeatureSelectionParameters().Describe());

    public NodeResult Execute(PipelineContext ctx, WorkflowNode node, PipelineTable input) =>
        ctx.FitTransform(node, input, full =>
        {
            var numeric = NumericFeatures(full.Schema, ctx.FeatureNames(input));
            if (numeric.Length == 0)
            {
                return (null, "");
            }

            var count = Math.Max(1, (int)ReadDouble(Cfg(node, "count"), 1));
            var est = ctx.Ml.Transforms.Concatenate("__FsIn", numeric)
                .Append(ctx.Ml.Transforms.FeatureSelection.SelectFeaturesBasedOnCount("Fs", "__FsIn", count: count))
                .Append(ctx.Ml.Transforms.DropColumns([.. numeric, "__FsIn"]));
            return (est, $"selected features (count ≥ {count})");
        }, "no numeric columns");
}
