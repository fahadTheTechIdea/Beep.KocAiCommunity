using Beep.KocAiCommunity.Application.ML;
using Beep.KocAiCommunity.Contracts.Workflow;
using static Beep.KocAiCommunity.ML.Nodes.NodeParam;
using static Beep.KocAiCommunity.ML.Nodes.PipelineContext;

namespace Beep.KocAiCommunity.ML.Nodes;

// DuckDB (SQL/ETL) node handlers. They operate on the DuckDB working table and run BEFORE the ML.NET
// modelling nodes — DuckDB is the data-prep front-end, not a replacement for the ML.NET engine.
// The working table is referenced in SQL as "working".

/// <summary>Runs an arbitrary SELECT over the working table (and any joined datasets), replacing it.</summary>
public sealed class SqlHandler : IPipelineNodeHandler
{
    public NodeEngine Engine => NodeEngine.Duck;
    public NodeDescriptor Descriptor { get; } = new("sql", "Data", "SQL query",
        "Transform the data with a SQL SELECT over the table `working`. Full DuckDB SQL — joins, "
        + "aggregations, window functions, CASE, etc. Keep the label column for downstream training.",
        PortKind.Table, PortKind.Table,
        [P("sql", "SELECT … FROM working", NodeParameterType.Text, required: true)]);

    public Task<NodeExecutionResult> ExecuteAsync(PipelineContext ctx, WorkflowNode node, CancellationToken ct)
    {
        var sql = Cfg(node, "sql");
        if (string.IsNullOrWhiteSpace(sql))
        {
            return Task.FromResult(new NodeExecutionResult(node.Id, node.Kind, "skipped", "no SQL provided"));
        }

        ctx.Duck!.ReplaceTable(WorkingTable, sql);
        return Task.FromResult(Done(ctx, node));
    }

    internal static NodeExecutionResult Done(PipelineContext ctx, WorkflowNode node)
    {
        var rows = ctx.Duck!.RowCount(WorkingTable);
        var cols = ctx.Duck.Columns(WorkingTable);
        return new NodeExecutionResult(node.Id, node.Kind, "done", $"{rows} rows · {cols.Count} cols: {string.Join(", ", cols)}");
    }
}

/// <summary>Keeps rows matching a SQL WHERE condition.</summary>
public sealed class SqlFilterHandler : IPipelineNodeHandler
{
    public NodeEngine Engine => NodeEngine.Duck;
    public NodeDescriptor Descriptor { get; } = new("sql-filter", "Data", "Filter (SQL)",
        "Keep only rows matching a SQL condition, e.g. pressure > 3000 AND zone = 'north'.",
        PortKind.Table, PortKind.Table,
        [P("where", "WHERE condition", NodeParameterType.Text, required: true)]);

    public Task<NodeExecutionResult> ExecuteAsync(PipelineContext ctx, WorkflowNode node, CancellationToken ct)
    {
        var where = Cfg(node, "where");
        if (string.IsNullOrWhiteSpace(where))
        {
            return Task.FromResult(new NodeExecutionResult(node.Id, node.Kind, "skipped", "no condition provided"));
        }

        ctx.Duck!.ReplaceTable(WorkingTable, $"SELECT * FROM {DuckDbSession.Quote(WorkingTable)} WHERE {where}");
        return Task.FromResult(SqlHandler.Done(ctx, node));
    }
}

/// <summary>Aggregates the working table (GROUP BY + aggregate expressions).</summary>
public sealed class GroupByHandler : IPipelineNodeHandler
{
    public NodeEngine Engine => NodeEngine.Duck;
    public NodeDescriptor Descriptor { get; } = new("group-by", "Data", "Group & aggregate",
        "Aggregate rows: pick group-by columns and aggregate expressions "
        + "(e.g. AVG(pressure) AS avg_p, MAX(vibration) AS max_v).",
        PortKind.Table, PortKind.Table,
        [P("groupBy", "Group-by columns", NodeParameterType.Columns, required: true),
         P("aggregations", "Aggregates, e.g. AVG(pressure) AS avg_p", NodeParameterType.Text, required: true)]);

    public Task<NodeExecutionResult> ExecuteAsync(PipelineContext ctx, WorkflowNode node, CancellationToken ct)
    {
        var groupBy = SplitList(Cfg(node, "groupBy"));
        var aggregations = Cfg(node, "aggregations");
        if (groupBy.Count == 0 || string.IsNullOrWhiteSpace(aggregations))
        {
            return Task.FromResult(new NodeExecutionResult(node.Id, node.Kind, "skipped", "group-by columns and aggregations are required"));
        }

        var keys = string.Join(", ", groupBy.Select(DuckDbSession.Quote));
        ctx.Duck!.ReplaceTable(WorkingTable, $"SELECT {keys}, {aggregations} FROM {DuckDbSession.Quote(WorkingTable)} GROUP BY {keys}");
        return Task.FromResult(SqlHandler.Done(ctx, node));
    }
}

/// <summary>Orders rows by one or more columns.</summary>
public sealed class SortHandler : IPipelineNodeHandler
{
    public NodeEngine Engine => NodeEngine.Duck;
    public NodeDescriptor Descriptor { get; } = new("sort", "Data", "Sort",
        "Order rows by columns, e.g. pressure DESC, well_id.", PortKind.Table, PortKind.Table,
        [P("orderBy", "ORDER BY, e.g. pressure DESC", NodeParameterType.Text, required: true)]);

    public Task<NodeExecutionResult> ExecuteAsync(PipelineContext ctx, WorkflowNode node, CancellationToken ct)
    {
        var orderBy = Cfg(node, "orderBy");
        if (string.IsNullOrWhiteSpace(orderBy))
        {
            return Task.FromResult(new NodeExecutionResult(node.Id, node.Kind, "skipped", "no ORDER BY provided"));
        }

        ctx.Duck!.ReplaceTable(WorkingTable, $"SELECT * FROM {DuckDbSession.Quote(WorkingTable)} ORDER BY {orderBy}");
        return Task.FromResult(SqlHandler.Done(ctx, node));
    }
}

/// <summary>Removes duplicate rows.</summary>
public sealed class DistinctHandler : IPipelineNodeHandler
{
    public NodeEngine Engine => NodeEngine.Duck;
    public NodeDescriptor Descriptor { get; } = new("distinct", "Data", "Deduplicate",
        "Remove duplicate rows (SELECT DISTINCT).", PortKind.Table, PortKind.Table, []);

    public Task<NodeExecutionResult> ExecuteAsync(PipelineContext ctx, WorkflowNode node, CancellationToken ct)
    {
        ctx.Duck!.ReplaceTable(WorkingTable, $"SELECT DISTINCT * FROM {DuckDbSession.Quote(WorkingTable)}");
        return Task.FromResult(SqlHandler.Done(ctx, node));
    }
}
