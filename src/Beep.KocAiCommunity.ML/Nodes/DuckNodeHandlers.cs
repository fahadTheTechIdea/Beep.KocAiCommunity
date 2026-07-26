using Beep.KocAiCommunity.Application.ML;
using Beep.KocAiCommunity.Contracts.Workflow;
using static Beep.KocAiCommunity.ML.Nodes.NodeParam;
using static Beep.KocAiCommunity.ML.Nodes.PipelineContext;

namespace Beep.KocAiCommunity.ML.Nodes;

// DuckDB (SQL/ETL) node handlers. Each loads the input table into a DuckDB table named "working",
// runs SQL, and returns the result as a new PipelineTable — the same contract as every other node.
// The working table is referenced in SQL as "working".

internal static class Duck
{
    // replay: whether this op re-applies to the fixed evaluation set at predict time. Column ops
    // (add/derive/join columns) do; row ops (filter/group/union/sort) must not touch the eval rows.
    public static NodeResult Run(PipelineContext ctx, WorkflowNode node, PipelineTable input, IPipelineNodeHandler self, string selectSql, bool replay)
    {
        input.LoadIntoDuck(ctx.Duck, WorkingTable);
        ctx.Duck.ReplaceTable(WorkingTable, selectSql);
        return Done(ctx, node, self, replay);
    }

    public static NodeResult Done(PipelineContext ctx, WorkflowNode node, IPipelineNodeHandler self, bool replay)
    {
        var output = PipelineTable.FromDuck(ctx.Duck, WorkingTable, ctx.TempFiles);
        return new NodeResult(
            new NodeExecutionResult(node.Id, node.Kind, "done", $"{output.RowCount} rows · {output.Columns.Count} cols: {string.Join(", ", output.Columns)}"),
            output,
            replay ? new DataReplay(node, self) : null);
    }

    public static NodeResult Skip(WorkflowNode node, string reason, PipelineTable input)
        => new(new NodeExecutionResult(node.Id, node.Kind, "skipped", reason), input);
}

public sealed class SqlHandler : IPipelineNodeHandler
{
    public NodeDescriptor Descriptor { get; } = new("sql", "Data", "SQL query",
        "Transform the data with a SQL SELECT over the table `working`. Full DuckDB SQL — joins, "
        + "aggregations, window functions, CASE, etc. Keep the label column for downstream training.",
        PortKind.Table, PortKind.Table,
        [P("sql", "SELECT … FROM working", NodeParameterType.Text, required: true)]);

    public NodeResult Execute(PipelineContext ctx, WorkflowNode node, PipelineTable input)
    {
        var sql = Cfg(node, "sql");
        return string.IsNullOrWhiteSpace(sql)
            ? Duck.Skip(node, "no SQL provided", input)
            : Duck.Run(ctx, node, input, this, sql, replay: true);
    }
}

public sealed class SqlFilterHandler : IPipelineNodeHandler
{
    public NodeDescriptor Descriptor { get; } = new("sql-filter", "Data", "Filter (SQL)",
        "Keep only rows matching a SQL condition, e.g. pressure > 3000 AND zone = 'north'.",
        PortKind.Table, PortKind.Table,
        [P("where", "WHERE condition", NodeParameterType.Text, required: true)]);

    public NodeResult Execute(PipelineContext ctx, WorkflowNode node, PipelineTable input)
    {
        var where = Cfg(node, "where");
        return string.IsNullOrWhiteSpace(where)
            ? Duck.Skip(node, "no condition provided", input)
            : Duck.Run(ctx, node, input, this, $"SELECT * FROM {DuckDbSession.Quote(WorkingTable)} WHERE {where}", replay: false);
    }
}

public sealed class GroupByHandler : IPipelineNodeHandler
{
    public NodeDescriptor Descriptor { get; } = new("group-by", "Data", "Group & aggregate",
        "Aggregate rows: pick group-by columns and aggregate expressions "
        + "(e.g. AVG(pressure) AS avg_p, MAX(vibration) AS max_v).",
        PortKind.Table, PortKind.Table,
        [P("groupBy", "Group-by columns", NodeParameterType.Columns, required: true),
         P("aggregations", "Aggregates, e.g. AVG(pressure) AS avg_p", NodeParameterType.Text, required: true)]);

    public NodeResult Execute(PipelineContext ctx, WorkflowNode node, PipelineTable input)
    {
        var groupBy = SplitList(Cfg(node, "groupBy"));
        var aggregations = Cfg(node, "aggregations");
        if (groupBy.Count == 0 || string.IsNullOrWhiteSpace(aggregations))
        {
            return Duck.Skip(node, "group-by columns and aggregations are required", input);
        }

        var keys = string.Join(", ", groupBy.Select(DuckDbSession.Quote));
        return Duck.Run(ctx, node, input, this, $"SELECT {keys}, {aggregations} FROM {DuckDbSession.Quote(WorkingTable)} GROUP BY {keys}", replay: false);
    }
}

public sealed class SortHandler : IPipelineNodeHandler
{
    public NodeDescriptor Descriptor { get; } = new("sort", "Data", "Sort",
        "Order rows by columns, e.g. pressure DESC, well_id.", PortKind.Table, PortKind.Table,
        [P("orderBy", "ORDER BY, e.g. pressure DESC", NodeParameterType.Text, required: true)]);

    public NodeResult Execute(PipelineContext ctx, WorkflowNode node, PipelineTable input)
    {
        var orderBy = Cfg(node, "orderBy");
        if (string.IsNullOrWhiteSpace(orderBy))
        {
            return Duck.Skip(node, "no ORDER BY provided", input);
        }

        // Append every column as a trailing tie-break so the sort is a total order: rows with equal
        // user keys still come out in one fixed, reproducible sequence rather than DuckDB's arbitrary
        // (and potentially run-varying) tie order. Redundant keys are harmless.
        var tieBreak = input.Columns.Count == 0
            ? string.Empty
            : ", " + string.Join(", ", input.Columns.Select(DuckDbSession.Quote));
        return Duck.Run(ctx, node, input, this, $"SELECT * FROM {DuckDbSession.Quote(WorkingTable)} ORDER BY {orderBy}{tieBreak}", replay: false);
    }
}

public sealed class DistinctHandler : IPipelineNodeHandler
{
    public NodeDescriptor Descriptor { get; } = new("distinct", "Data", "Deduplicate",
        "Remove duplicate rows (SELECT DISTINCT).", PortKind.Table, PortKind.Table, []);

    public NodeResult Execute(PipelineContext ctx, WorkflowNode node, PipelineTable input)
        => Duck.Run(ctx, node, input, this, $"SELECT DISTINCT * FROM {DuckDbSession.Quote(WorkingTable)}", replay: false);
}

public sealed class JoinDatasetHandler : IPipelineNodeHandler
{
    public NodeDescriptor Descriptor { get; } = new("join-dataset", "Data", "Join another dataset",
        "Bring in columns from a second dataset by matching a shared key column (a left join).",
        PortKind.Table, PortKind.Table,
        [P("datasetId", "Dataset to join", NodeParameterType.Dataset, required: true),
         P("on", "Key column (in both)", NodeParameterType.Text, required: true),
         P("columns", "Columns to bring (blank = all)", NodeParameterType.Columns)]);

    public NodeResult Execute(PipelineContext ctx, WorkflowNode node, PipelineTable input)
    {
        if (!Guid.TryParse(Cfg(node, "datasetId"), out var id) || ctx.SecondaryTable(id) is not { } otherTable)
        {
            return Duck.Skip(node, "no dataset selected (or it could not be loaded)", input);
        }

        var on = Cfg(node, "on");
        if (string.IsNullOrWhiteSpace(on))
        {
            return Duck.Skip(node, "a key column is required", input);
        }

        input.LoadIntoDuck(ctx.Duck, WorkingTable);
        var wanted = SplitList(Cfg(node, "columns"));
        var otherCols = ctx.Duck.Columns(otherTable).Where(c => c != on && (wanted.Count == 0 || wanted.Contains(c))).ToList();
        var select = otherCols.Count == 0
            ? "w.*"
            : "w.*, " + string.Join(", ", otherCols.Select(c => $"o.{DuckDbSession.Quote(c)} AS {DuckDbSession.Quote(c)}"));

        ctx.Duck.ReplaceTable(WorkingTable,
            $"SELECT {select} FROM {DuckDbSession.Quote(WorkingTable)} w LEFT JOIN {DuckDbSession.Quote(otherTable)} o ON w.{DuckDbSession.Quote(on)} = o.{DuckDbSession.Quote(on)}");

        var result = Duck.Done(ctx, node, this, replay: true);
        return result with { Status = result.Status with { Detail = $"joined {otherCols.Count} column(s) on {on} · {result.Status.Detail}" } };
    }
}

public sealed class UnionDatasetHandler : IPipelineNodeHandler
{
    public NodeDescriptor Descriptor { get; } = new("union-dataset", "Data", "Append another dataset",
        "Add the rows of a second dataset to the current data (columns aligned by name; missing ones become null).",
        PortKind.Table, PortKind.Table,
        [P("datasetId", "Dataset to append", NodeParameterType.Dataset, required: true)]);

    public NodeResult Execute(PipelineContext ctx, WorkflowNode node, PipelineTable input)
    {
        if (!Guid.TryParse(Cfg(node, "datasetId"), out var id) || ctx.SecondaryTable(id) is not { } otherTable)
        {
            return Duck.Skip(node, "no dataset selected (or it could not be loaded)", input);
        }

        // If the current data is labelled, the appended dataset must carry the same label — otherwise
        // UNION ALL BY NAME gives its rows a NULL label and they'd train as a phantom class. Fail loudly.
        if (input.HasColumn(ctx.LabelColumn) && !ctx.Duck.Columns(otherTable).Contains(ctx.LabelColumn))
        {
            return new NodeResult(new NodeExecutionResult(node.Id, node.Kind, "failed",
                $"the appended dataset has no '{ctx.LabelColumn}' column, so its rows would train with a null "
                + "label. Add the label column to that dataset, or don't append it."), input);
        }

        input.LoadIntoDuck(ctx.Duck, WorkingTable);
        ctx.Duck.ReplaceTable(WorkingTable,
            $"SELECT * FROM {DuckDbSession.Quote(WorkingTable)} UNION ALL BY NAME SELECT * FROM {DuckDbSession.Quote(otherTable)}");

        // If a split already tagged rows, the appended dataset has no fold marker, so BY NAME leaves those
        // rows NULL — and the fold views filter NULL out of BOTH train and test, silently discarding every
        // appended row. Assign them to the train fold (0) so they are used, never dropped without a trace.
        if (ctx.Duck.Columns(WorkingTable).Any(c => c == FoldColumn))
        {
            ctx.Duck.Execute($"UPDATE {DuckDbSession.Quote(WorkingTable)} SET {DuckDbSession.Quote(FoldColumn)} = 0 WHERE {DuckDbSession.Quote(FoldColumn)} IS NULL;");
        }

        return Duck.Done(ctx, node, this, replay: false);
    }
}
