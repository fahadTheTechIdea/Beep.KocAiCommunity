using Beep.KocAiCommunity.Application.ML;
using Beep.KocAiCommunity.Contracts.Workflow;
using Microsoft.ML;
using Microsoft.ML.Data;
using static Beep.KocAiCommunity.ML.Nodes.PipelineContext;

namespace Beep.KocAiCommunity.ML.Nodes;

// Prepare (data-management) + Shape (row-op) handlers, uniform contract.

public sealed class RenameColumnHandler : IPipelineNodeHandler
{
    public NodeDescriptor Descriptor { get; } = new("rename-column", "Prepare", "Rename column",
        "Give a feature a clearer name (e.g. WHP → wellhead_pressure).", PortKind.Table, PortKind.Table,
        new RenameColumnParameters().Describe());

    public NodeResult Execute(PipelineContext ctx, WorkflowNode node, PipelineTable input)
    {
        var from = Cfg(node, "from");
        var to = Cfg(node, "to");
        if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
        {
            return new NodeResult(new NodeExecutionResult(node.Id, node.Kind, "skipped", "'from' and 'to' are required"), input);
        }

        if (!input.HasColumn(from))
        {
            return new NodeResult(new NodeExecutionResult(node.Id, node.Kind, "skipped", $"column '{from}' not found"), input);
        }

        return ctx.FitTransform(node, input,
            _ => (ctx.Ml.Transforms.CopyColumns(to, from).Append(ctx.Ml.Transforms.DropColumns(from)), $"{from} → {to}"),
            "'from' and 'to' are required");
    }
}

public sealed class ConvertNumericHandler : IPipelineNodeHandler
{
    public NodeDescriptor Descriptor { get; } = new("convert-numeric", "Prepare", "Cast to number",
        "Convert text/typed columns to numbers so they can be used as features.", PortKind.Table, PortKind.Table,
        new ConvertNumericParameters().Describe());

    public NodeResult Execute(PipelineContext ctx, WorkflowNode node, PipelineTable input) =>
        ctx.FitTransform(node, input, full =>
        {
            var wanted = SplitList(Cfg(node, "columns"));
            var target = (wanted.Count > 0 ? ctx.FeatureNames(input).Where(wanted.Contains) : TextFeatures(full.Schema, ctx.FeatureNames(input))).ToArray();
            return target.Length == 0 ? (null, "") : (ctx.Ml.Transforms.Conversion.ConvertType([.. target.Select(c => new InputOutputColumnPair(c))], DataKind.Single), $"cast to number: {string.Join(", ", target)}");
        }, "no columns to convert");
}

public sealed class ComputeColumnHandler : IPipelineNodeHandler
{
    public NodeDescriptor Descriptor { get; } = new("compute-column", "Prepare", "Compute column",
        "Create a new column from a formula, e.g. gor = gas / (oil + 1). Params bind to the input columns in order.",
        PortKind.Table, PortKind.Table,
        new ComputeColumnParameters().Describe());

    public NodeResult Execute(PipelineContext ctx, WorkflowNode node, PipelineTable input)
    {
        // Building a feature FROM the label (or id) is target leakage: unlike auto feature-selection,
        // this node takes an explicit input list that isn't filtered through FeatureNames, so it could
        // otherwise encode the target into a feature (and it would then break at predict, where the eval
        // set has no label). Reject it loudly. Also the derived column would itself be replayed on the
        // label-less eval set, so it simply cannot depend on the label.
        var requested = SplitList(Cfg(node, "inputs"));
        var leaky = requested.Where(c => c == ctx.LabelColumn || (ctx.IdColumn is not null && c == ctx.IdColumn)).ToList();
        if (leaky.Count > 0)
        {
            return new NodeResult(new NodeExecutionResult(node.Id, node.Kind, "failed",
                $"'{string.Join(", ", leaky)}' is the label/id column — deriving a feature from the target is data leakage. "
                + "Use feature columns only."), input);
        }

        return ctx.FitTransform(node, input, _ =>
        {
            var output = Cfg(node, "output");
            var expression = Cfg(node, "expression");
            var inputs = requested.Where(input.HasColumn).ToArray();
            if (string.IsNullOrWhiteSpace(output) || string.IsNullOrWhiteSpace(expression) || inputs.Length == 0)
            {
                return (null, "");
            }

            var est = ctx.Ml.Transforms.Conversion.ConvertType([.. inputs.Select(c => new InputOutputColumnPair(c))], DataKind.Single)
                .Append(ctx.Ml.Transforms.Expression(output, expression, inputs));
            return (est, $"{output} = {expression}  [{string.Join(", ", inputs)}]");
        }, "'output', 'inputs' and 'expression' are required");
    }
}

public sealed class CombineColumnsHandler : IPipelineNodeHandler
{
    public NodeDescriptor Descriptor { get; } = new("combine-columns", "Prepare", "Merge columns",
        "Combine several numeric columns into one feature vector.", PortKind.Table, PortKind.Table,
        new CombineColumnsParameters().Describe());

    public NodeResult Execute(PipelineContext ctx, WorkflowNode node, PipelineTable input) =>
        ctx.FitTransform(node, input, full =>
        {
            var wanted = SplitList(Cfg(node, "columns"));
            var target = (wanted.Count > 0 ? ctx.FeatureNames(input).Where(wanted.Contains) : NumericFeatures(full.Schema, ctx.FeatureNames(input))).ToArray();
            if (target.Length < 2)
            {
                return (null, "");
            }

            return (ctx.Ml.Transforms.Concatenate("Combined", target).Append(ctx.Ml.Transforms.DropColumns(target)), $"combined {target.Length} → Combined");
        }, "need ≥2 columns to combine");
}

public sealed class LpNormalizeHandler : IPipelineNodeHandler
{
    public NodeDescriptor Descriptor { get; } = new("lp-normalize", "Prepare", "Lp-normalize",
        "Scale each row's feature vector to unit norm — good for magnitude-invariant signals.", PortKind.Table, PortKind.Table, new LpNormalizeParameters().Describe());

    public NodeResult Execute(PipelineContext ctx, WorkflowNode node, PipelineTable input) =>
        VectorNormalize(ctx, node, input, "__LpIn", "LpNorm", (o, i) => ctx.Ml.Transforms.NormalizeLpNorm(o, i), "Lp-normalized feature vector");

    internal static NodeResult VectorNormalize(PipelineContext ctx, WorkflowNode node, PipelineTable input,
        string inVec, string outVec, Func<string, string, Microsoft.ML.IEstimator<Microsoft.ML.ITransformer>> norm, string detail) =>
        ctx.FitTransform(node, input, full =>
        {
            var numeric = NumericFeatures(full.Schema, ctx.FeatureNames(input));
            if (numeric.Length == 0)
            {
                return (null, "");
            }

            var est = ctx.Ml.Transforms.Concatenate(inVec, numeric)
                .Append(norm(outVec, inVec))
                .Append(ctx.Ml.Transforms.DropColumns([.. numeric, inVec]));
            return (est, detail);
        }, "no numeric columns");
}

public sealed class GlobalContrastHandler : IPipelineNodeHandler
{
    public NodeDescriptor Descriptor { get; } = new("global-contrast", "Prepare", "Global contrast",
        "Centre and scale each row's features (global contrast normalization).", PortKind.Table, PortKind.Table, new GlobalContrastParameters().Describe());

    public NodeResult Execute(PipelineContext ctx, WorkflowNode node, PipelineTable input) =>
        LpNormalizeHandler.VectorNormalize(ctx, node, input, "__GcnIn", "Gcn", (o, i) => ctx.Ml.Transforms.NormalizeGlobalContrast(o, i), "global-contrast normalized");
}

public sealed class TakeRowsHandler : IPipelineNodeHandler
{
    public NodeDescriptor Descriptor { get; } = new("take-rows", "Shape", "Take first N",
        "Keep only the first N rows (quick experiments on big data).", PortKind.Table, PortKind.Table,
        new TakeRowsParameters().Describe());

    public NodeResult Execute(PipelineContext ctx, WorkflowNode node, PipelineTable input)
    {
        if (RowSampleAfterSplit(ctx, node) is { } rejected)
        {
            return rejected;
        }

        var n = Math.Max(1, (int)ReadDouble(Cfg(node, "count"), 1000));
        var full = input.LoadIntoMl(ctx.Ml, ctx.LabelColumn);
        var output = PipelineTable.FromMlView(ctx.Ml.Data.TakeRows(full, n), ctx.TempFiles);
        return new NodeResult(new NodeExecutionResult(node.Id, node.Kind, "done", $"kept first {output.RowCount} of {input.RowCount}"), output);
    }

    // 'split' writes all train rows then all test rows into one table, so a count/position-based row op
    // after it can silently drop an entire fold — take-rows(N ≤ trainCount) yields only train rows, leaving
    // 'evaluate' with no held-out set. Reject it: such sampling belongs before the split.
    internal static NodeResult? RowSampleAfterSplit(PipelineContext ctx, WorkflowNode node)
        => ctx.HasSplit
            ? new NodeResult(new NodeExecutionResult(node.Id, node.Kind, "failed",
                "row sampling after 'split' would drop rows from the held-out test set (split lays out train "
                + "rows then test rows). Place take-rows/sample before the split."), null)
            : null;
}

public sealed class ShuffleHandler : IPipelineNodeHandler
{
    public NodeDescriptor Descriptor { get; } = new("shuffle", "Shape", "Shuffle rows",
        "Randomly reorder the rows (deterministic seed).", PortKind.Table, PortKind.Table, new ShuffleParameters().Describe());

    public NodeResult Execute(PipelineContext ctx, WorkflowNode node, PipelineTable input)
    {
        var full = input.LoadIntoMl(ctx.Ml, ctx.LabelColumn);
        var output = PipelineTable.FromMlView(ctx.Ml.Data.ShuffleRows(full, seed: 1), ctx.TempFiles);
        return new NodeResult(new NodeExecutionResult(node.Id, node.Kind, "done", "rows shuffled"), output);
    }
}

public sealed class SampleHandler : IPipelineNodeHandler
{
    public NodeDescriptor Descriptor { get; } = new("sample", "Shape", "Sample rows",
        "Take a random fraction of the rows.", PortKind.Table, PortKind.Table,
        new SampleParameters().Describe());

    public NodeResult Execute(PipelineContext ctx, WorkflowNode node, PipelineTable input)
    {
        if (TakeRowsHandler.RowSampleAfterSplit(ctx, node) is { } rejected)
        {
            return rejected;
        }

        var fraction = ReadDouble(Cfg(node, "fraction"), 0.5);
        var full = input.LoadIntoMl(ctx.Ml, ctx.LabelColumn);
        var take = Math.Max(1, (long)(input.RowCount * fraction));
        var output = PipelineTable.FromMlView(ctx.Ml.Data.TakeRows(ctx.Ml.Data.ShuffleRows(full, seed: 1), take), ctx.TempFiles);
        return new NodeResult(new NodeExecutionResult(node.Id, node.Kind, "done", $"{output.RowCount} of {input.RowCount} rows ({fraction:0.##})"), output);
    }
}

public sealed class FilterRowsHandler : IPipelineNodeHandler
{
    public NodeDescriptor Descriptor { get; } = new("filter-rows", "Shape", "Filter rows",
        "Keep rows where a numeric column falls in a range.", PortKind.Table, PortKind.Table,
        new FilterRowsParameters().Describe());

    public NodeResult Execute(PipelineContext ctx, WorkflowNode node, PipelineTable input)
    {
        var column = Cfg(node, "column");
        if (string.IsNullOrWhiteSpace(column))
        {
            return new NodeResult(new NodeExecutionResult(node.Id, node.Kind, "skipped", "no column configured"), input);
        }

        // 'column' is a free-text param, so the executor's Columns validation doesn't cover it — check it
        // here so a typo fails with a clear message instead of a raw ML.NET "column not found" exception.
        if (!input.HasColumn(column))
        {
            return new NodeResult(new NodeExecutionResult(node.Id, node.Kind, "failed",
                $"column '{column}' not found — check the name."), input);
        }

        var min = ReadDouble(Cfg(node, "min"), double.NegativeInfinity);
        var max = ReadDouble(Cfg(node, "max"), double.PositiveInfinity);
        var full = input.LoadIntoMl(ctx.Ml, ctx.LabelColumn);
        var output = PipelineTable.FromMlView(ctx.Ml.Data.FilterRowsByColumn(full, column, lowerBound: min, upperBound: max), ctx.TempFiles);
        return new NodeResult(new NodeExecutionResult(node.Id, node.Kind, "done", $"{output.RowCount} of {input.RowCount} rows kept"), output);
    }
}
