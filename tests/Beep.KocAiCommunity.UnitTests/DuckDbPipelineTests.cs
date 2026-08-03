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
// Serialised with every other AutoML test. Training gets a fixed wall-clock budget, so when
// several of these run at once they starve each other of cores, complete fewer trials, and fail
// on a worse model than the same test produces alone — which reads as flakiness rather than as
// contention. Slower in total, and honest.
[Collection(MlTrainingCollection.Name)]
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
    public void Sort_breaks_ties_deterministically_into_a_total_order()
    {
        // Many rows share the primary sort key 'k' but differ on 'v'. Without a total-order tie-break the
        // engine could emit tied rows in an arbitrary (run-varying) sequence; the sort node appends every
        // column so the output is one fixed order — verified stable across two independent sessions.
        var inPath = Path.Combine(Path.GetTempPath(), $"sort-in-{Guid.NewGuid():N}.csv");
        var rows = Enumerable.Range(0, 200).Select(i => $"{i % 3},{199 - i}");
        File.WriteAllText(inPath, "k,v\n" + string.Join("\n", rows) + "\n");
        try
        {
            var first = RunSort(inPath, "k");
            var second = RunSort(inPath, "k");

            first.Should().Equal(second, "the sort must be reproducible run-to-run");
            // Total order: ascending by k, then (tie-break) ascending by v.
            first.Should().Equal(first.OrderBy(r => r.K).ThenBy(r => r.V).ToList());
        }
        finally
        {
            File.Delete(inPath);
        }
    }

    private static List<(int K, int V)> RunSort(string inPath, string orderBy)
    {
        using var ctx = new PipelineContext
        {
            Ml = new Microsoft.ML.MLContext(seed: 1),
            Task = MlTaskType.BinaryClassification,
            Mode = PipelineMode.Execute,
            LabelColumn = "label",
        };
        var node = new WorkflowNode { Id = "s", Kind = "sort", Config = new Dictionary<string, string> { ["orderBy"] = orderBy } };
        var result = new SortHandler().Execute(ctx, node, PipelineTable.FromCsvFile(inPath));

        return File.ReadAllLines(result.Output!.CsvPath).Skip(1).Where(l => l.Length > 0)
            .Select(l => l.Split(',')).Select(p => (int.Parse(p[0]), int.Parse(p[1]))).ToList();
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
