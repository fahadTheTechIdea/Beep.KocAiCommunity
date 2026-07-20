using System.Text;
using Beep.KocAiCommunity.Application.ML;
using Beep.KocAiCommunity.Contracts.Workflow;
using Beep.KocAiCommunity.ML.Nodes;
using FluentAssertions;
using Xunit;

namespace Beep.KocAiCommunity.UnitTests;

/// <summary>
/// DuckDB data-prep nodes feeding the ML.NET modelling nodes in one graph — DuckDB is the ETL
/// front-end, ML.NET does the training (the two engines cross once via CSV).
/// </summary>
public class DuckDbPipelineTests
{
    private static PluginNodeExecutor NewExecutor()
    {
        var handlers = typeof(PluginNodeExecutor).Assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false } && typeof(IPipelineNodeHandler).IsAssignableFrom(t))
            .Select(t => (IPipelineNodeHandler)Activator.CreateInstance(t)!);
        return new PluginNodeExecutor(new PluginNodeRegistry(handlers));
    }

    private static MemoryStream SeparableCsv()
    {
        var sb = new StringBuilder("x1,x2,zone,label\n");
        for (var i = 0; i < 80; i++)
        {
            sb.Append($"{7 + (i % 3)},{7 + ((i / 3) % 3)},north,true\n");
            sb.Append($"{i % 3},{(i / 3) % 3},south,false\n");
        }

        return new MemoryStream(Encoding.UTF8.GetBytes(sb.ToString()));
    }

    [Fact]
    public async Task DuckDb_sql_prep_then_mlnet_train_and_evaluate()
    {
        // dataset -> SQL (derive a feature, keep the label) -> filter -> split -> train -> evaluate.
        var def = new WorkflowDefinition
        {
            Name = "duck-then-ml",
            Nodes =
            [
                new() { Id = "d", Kind = "dataset" },
                new() { Id = "q", Kind = "sql", Config = new Dictionary<string, string> { ["sql"] = "SELECT *, x1 + x2 AS total FROM working" } },
                new() { Id = "f", Kind = "sql-filter", Config = new Dictionary<string, string> { ["where"] = "x1 >= 0" } },
                new() { Id = "sp", Kind = "split" },
                new() { Id = "tr", Kind = "train" },
                new() { Id = "ev", Kind = "evaluate" },
            ],
            Edges = [new("d", "q"), new("q", "f"), new("f", "sp"), new("sp", "tr"), new("tr", "ev")],
        };

        using var csv = SeparableCsv();
        var result = await NewExecutor().ExecuteAsync(def, "label", MlTaskType.BinaryClassification, csv, 5);

        var failed = result.Nodes.FirstOrDefault(n => n.Status is not "done" and not "skipped");
        result.Success.Should().BeTrue($"but node '{failed?.Kind}' {failed?.Status}: {failed?.Detail}");
        result.Nodes.Single(n => n.Kind == "sql").Detail.Should().Contain("total");
        result.Nodes.Single(n => n.Kind == "train").Status.Should().Be("done");
        result.PrimaryValue.Should().BeGreaterThan(0.8);
    }

    [Fact]
    public async Task Duck_and_ml_nodes_are_freely_ordered()
    {
        // A DuckDB node AFTER an ML.NET transform — proves the uniform table contract removes the
        // ordering constraint: normalize (ML) → sql derive (DuckDB) → split → train.
        var def = new WorkflowDefinition
        {
            Name = "mixed-order",
            Nodes =
            [
                new() { Id = "d", Kind = "dataset" },
                new() { Id = "nz", Kind = "normalize" },
                new() { Id = "q", Kind = "sql", Config = new Dictionary<string, string> { ["sql"] = "SELECT *, x1 * x2 AS product FROM working" } },
                new() { Id = "sp", Kind = "split" },
                new() { Id = "tr", Kind = "train" },
                new() { Id = "ev", Kind = "evaluate" },
            ],
            Edges = [new("d", "nz"), new("nz", "q"), new("q", "sp"), new("sp", "tr"), new("tr", "ev")],
        };

        using var csv = SeparableCsv();
        var result = await NewExecutor().ExecuteAsync(def, "label", MlTaskType.BinaryClassification, csv, 5);

        var failed = result.Nodes.FirstOrDefault(n => n.Status is not "done" and not "skipped");
        result.Success.Should().BeTrue($"but node '{failed?.Kind}' {failed?.Status}: {failed?.Detail}");
        result.Nodes.Single(n => n.Kind == "sql").Detail.Should().Contain("product");
        result.PrimaryValue.Should().BeGreaterThan(0.8);
    }

    [Fact]
    public async Task DuckDb_group_by_and_sort_chain()
    {
        // A DuckDB-only ETL chain: aggregate per zone, then sort — proves multi-node SQL flow.
        var def = new WorkflowDefinition
        {
            Name = "group",
            Nodes =
            [
                new() { Id = "d", Kind = "dataset" },
                new() { Id = "g", Kind = "group-by", Config = new Dictionary<string, string> { ["groupBy"] = "x1", ["aggregations"] = "AVG(x2) AS avg_x2, COUNT(*) AS n" } },
                new() { Id = "s", Kind = "sort", Config = new Dictionary<string, string> { ["orderBy"] = "avg_x2 DESC" } },
                new() { Id = "cl", Kind = "cluster", Config = new Dictionary<string, string> { ["clusters"] = "2" } },
            ],
            Edges = [new("d", "g"), new("g", "s"), new("s", "cl")],
        };

        using var csv = SeparableCsv();
        var result = await NewExecutor().ExecuteAsync(def, "label", MlTaskType.BinaryClassification, csv, 5);

        var failed = result.Nodes.FirstOrDefault(n => n.Status is "failed");
        result.Success.Should().BeTrue($"but node '{failed?.Kind}': {failed?.Detail}");
        result.Nodes.Single(n => n.Kind == "group-by").Detail.Should().Contain("avg_x2");
        result.Nodes.Single(n => n.Kind == "cluster").Detail.Should().Contain("clusters");
    }
}
