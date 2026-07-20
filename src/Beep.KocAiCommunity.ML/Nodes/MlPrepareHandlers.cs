using Beep.KocAiCommunity.Application.ML;
using Beep.KocAiCommunity.Contracts.Workflow;
using Microsoft.ML;
using Microsoft.ML.Data;
using static Beep.KocAiCommunity.ML.Nodes.NodeParam;
using static Beep.KocAiCommunity.ML.Nodes.PipelineContext;

namespace Beep.KocAiCommunity.ML.Nodes;

// Prepare (data-management) + Shape (row-op) handlers — ported verbatim from the monolithic executor.

public sealed class RenameColumnHandler : IPipelineNodeHandler
{
    public NodeEngine Engine => NodeEngine.Ml;
    public NodeDescriptor Descriptor { get; } = new("rename-column", "Prepare", "Rename column",
        "Give a feature a clearer name (e.g. WHP → wellhead_pressure).", PortKind.Table, PortKind.Table,
        [P("from", "From column", NodeParameterType.Text, required: true), P("to", "New name", NodeParameterType.Text, required: true)]);

    public Task<NodeExecutionResult> ExecuteAsync(PipelineContext ctx, WorkflowNode node, CancellationToken ct)
    {
        var from = Cfg(node, "from");
        var to = Cfg(node, "to");
        if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
        {
            return Task.FromResult(new NodeExecutionResult(node.Id, node.Kind, "skipped", "'from' and 'to' are required"));
        }

        if (!ctx.FeatureColumns.Contains(from))
        {
            return Task.FromResult(new NodeExecutionResult(node.Id, node.Kind, "skipped", $"column '{from}' not found"));
        }

        var t = ctx.Ml.Transforms.CopyColumns(to, from).Append(ctx.Ml.Transforms.DropColumns(from)).Fit(ctx.TrainView);
        ctx.TrainView = t.Transform(ctx.TrainView);
        ctx.Preprocessors.Add(t);
        ctx.FeatureColumns = ctx.FeatureColumns.Select(c => c == from ? to : c).ToList();
        return Task.FromResult(new NodeExecutionResult(node.Id, node.Kind, "done", $"{from} → {to}"));
    }
}

public sealed class ConvertNumericHandler : IPipelineNodeHandler
{
    public NodeEngine Engine => NodeEngine.Ml;
    public NodeDescriptor Descriptor { get; } = new("convert-numeric", "Prepare", "Cast to number",
        "Convert text/typed columns to numbers so they can be used as features.", PortKind.Table, PortKind.Table,
        [P("columns", "Columns (blank = all text)", NodeParameterType.Columns)]);

    public Task<NodeExecutionResult> ExecuteAsync(PipelineContext ctx, WorkflowNode node, CancellationToken ct)
    {
        var wanted = SplitList(Cfg(node, "columns"));
        var target = (wanted.Count > 0 ? ctx.FeatureColumns.Where(wanted.Contains) : TextFeatures(ctx.TrainView.Schema, ctx.FeatureColumns)).ToArray();
        if (target.Length == 0)
        {
            return Task.FromResult(new NodeExecutionResult(node.Id, node.Kind, "skipped", "no columns to convert"));
        }

        var t = ctx.Ml.Transforms.Conversion.ConvertType([.. target.Select(c => new InputOutputColumnPair(c))], DataKind.Single).Fit(ctx.TrainView);
        ctx.TrainView = t.Transform(ctx.TrainView);
        ctx.Preprocessors.Add(t);
        return Task.FromResult(new NodeExecutionResult(node.Id, node.Kind, "done", $"cast to number: {string.Join(", ", target)}"));
    }
}

public sealed class ComputeColumnHandler : IPipelineNodeHandler
{
    public NodeEngine Engine => NodeEngine.Ml;
    public NodeDescriptor Descriptor { get; } = new("compute-column", "Prepare", "Compute column",
        "Create a new column from a formula, e.g. gor = gas / (oil + 1). Params bind to the input columns in order.",
        PortKind.Table, PortKind.Table,
        [P("output", "New column name", NodeParameterType.Text, required: true),
         P("inputs", "Input columns", NodeParameterType.Columns, required: true),
         P("expression", "Formula, e.g. (gas, oil) => gas / (oil + 1)", NodeParameterType.Text, required: true)]);

    public Task<NodeExecutionResult> ExecuteAsync(PipelineContext ctx, WorkflowNode node, CancellationToken ct)
    {
        var output = Cfg(node, "output");
        var expression = Cfg(node, "expression");
        var inputs = SplitList(Cfg(node, "inputs")).Where(ctx.FeatureColumns.Contains).ToArray();
        if (string.IsNullOrWhiteSpace(output) || string.IsNullOrWhiteSpace(expression))
        {
            return Task.FromResult(new NodeExecutionResult(node.Id, node.Kind, "skipped", "'output' name and 'expression' are required"));
        }

        if (inputs.Length == 0)
        {
            return Task.FromResult(new NodeExecutionResult(node.Id, node.Kind, "skipped", "no valid input columns"));
        }

        var cast = ctx.Ml.Transforms.Conversion.ConvertType([.. inputs.Select(c => new InputOutputColumnPair(c))], DataKind.Single);
        var t = cast.Append(ctx.Ml.Transforms.Expression(output, expression, inputs)).Fit(ctx.TrainView);
        ctx.TrainView = t.Transform(ctx.TrainView);
        ctx.Preprocessors.Add(t);
        if (!ctx.FeatureColumns.Contains(output))
        {
            ctx.FeatureColumns = [.. ctx.FeatureColumns, output];
        }

        return Task.FromResult(new NodeExecutionResult(node.Id, node.Kind, "done", $"{output} = {expression}  [{string.Join(", ", inputs)}]"));
    }
}

public sealed class CombineColumnsHandler : IPipelineNodeHandler
{
    public NodeEngine Engine => NodeEngine.Ml;
    public NodeDescriptor Descriptor { get; } = new("combine-columns", "Prepare", "Merge columns",
        "Combine several numeric columns into one feature vector.", PortKind.Table, PortKind.Table,
        [P("columns", "Columns (blank = all numeric)", NodeParameterType.Columns)]);

    public Task<NodeExecutionResult> ExecuteAsync(PipelineContext ctx, WorkflowNode node, CancellationToken ct)
    {
        var wanted = SplitList(Cfg(node, "columns"));
        var target = (wanted.Count > 0 ? ctx.FeatureColumns.Where(wanted.Contains) : ctx.NumericFeatures()).ToArray();
        if (target.Length < 2)
        {
            return Task.FromResult(new NodeExecutionResult(node.Id, node.Kind, "skipped", "need ≥2 columns to combine"));
        }

        var t = ctx.Ml.Transforms.Concatenate("Combined", target).Fit(ctx.TrainView);
        ctx.TrainView = t.Transform(ctx.TrainView);
        ctx.Preprocessors.Add(t);
        ctx.FeatureColumns = [.. ctx.FeatureColumns.Where(c => !target.Contains(c)), "Combined"];
        return Task.FromResult(new NodeExecutionResult(node.Id, node.Kind, "done", $"combined {target.Length} → Combined"));
    }
}

public sealed class LpNormalizeHandler : IPipelineNodeHandler
{
    public NodeEngine Engine => NodeEngine.Ml;
    public NodeDescriptor Descriptor { get; } = new("lp-normalize", "Prepare", "Lp-normalize",
        "Scale each row's feature vector to unit norm — good for magnitude-invariant signals.", PortKind.Table, PortKind.Table, []);

    public Task<NodeExecutionResult> ExecuteAsync(PipelineContext ctx, WorkflowNode node, CancellationToken ct)
    {
        var numeric = ctx.NumericFeatures();
        if (numeric.Length == 0)
        {
            return Task.FromResult(new NodeExecutionResult(node.Id, node.Kind, "skipped", "no numeric columns"));
        }

        var t = ctx.Ml.Transforms.Concatenate("__LpIn", numeric)
            .Append(ctx.Ml.Transforms.NormalizeLpNorm("LpNorm", "__LpIn"))
            .Fit(ctx.TrainView);
        ctx.TrainView = t.Transform(ctx.TrainView);
        ctx.Preprocessors.Add(t);
        ctx.FeatureColumns = ["LpNorm"];
        return Task.FromResult(new NodeExecutionResult(node.Id, node.Kind, "done", "Lp-normalized feature vector"));
    }
}

public sealed class GlobalContrastHandler : IPipelineNodeHandler
{
    public NodeEngine Engine => NodeEngine.Ml;
    public NodeDescriptor Descriptor { get; } = new("global-contrast", "Prepare", "Global contrast",
        "Centre and scale each row's features (global contrast normalization).", PortKind.Table, PortKind.Table, []);

    public Task<NodeExecutionResult> ExecuteAsync(PipelineContext ctx, WorkflowNode node, CancellationToken ct)
    {
        var numeric = ctx.NumericFeatures();
        if (numeric.Length == 0)
        {
            return Task.FromResult(new NodeExecutionResult(node.Id, node.Kind, "skipped", "no numeric columns"));
        }

        var t = ctx.Ml.Transforms.Concatenate("__GcnIn", numeric)
            .Append(ctx.Ml.Transforms.NormalizeGlobalContrast("Gcn", "__GcnIn"))
            .Fit(ctx.TrainView);
        ctx.TrainView = t.Transform(ctx.TrainView);
        ctx.Preprocessors.Add(t);
        ctx.FeatureColumns = ["Gcn"];
        return Task.FromResult(new NodeExecutionResult(node.Id, node.Kind, "done", "global-contrast normalized"));
    }
}

public sealed class TakeRowsHandler : IPipelineNodeHandler
{
    public NodeEngine Engine => NodeEngine.Ml;
    public NodeDescriptor Descriptor { get; } = new("take-rows", "Shape", "Take first N",
        "Keep only the first N training rows (quick experiments on big data).", PortKind.Table, PortKind.Table,
        [P("count", "Rows to keep", NodeParameterType.Number, def: "1000")]);

    public Task<NodeExecutionResult> ExecuteAsync(PipelineContext ctx, WorkflowNode node, CancellationToken ct)
    {
        var n = Math.Max(1, (int)ReadDouble(Cfg(node, "count"), 1000));
        var before = Count(ctx.TrainView);
        ctx.TrainView = ctx.Ml.Data.TakeRows(ctx.TrainView, n);
        return Task.FromResult(new NodeExecutionResult(node.Id, node.Kind, "done", $"kept first {Count(ctx.TrainView)} of {before}"));
    }
}

public sealed class ShuffleHandler : IPipelineNodeHandler
{
    public NodeEngine Engine => NodeEngine.Ml;
    public NodeDescriptor Descriptor { get; } = new("shuffle", "Shape", "Shuffle rows",
        "Randomly reorder the training rows (deterministic seed).", PortKind.Table, PortKind.Table, []);

    public Task<NodeExecutionResult> ExecuteAsync(PipelineContext ctx, WorkflowNode node, CancellationToken ct)
    {
        ctx.TrainView = ctx.Ml.Data.ShuffleRows(ctx.TrainView, seed: 1);
        return Task.FromResult(new NodeExecutionResult(node.Id, node.Kind, "done", "rows shuffled"));
    }
}
