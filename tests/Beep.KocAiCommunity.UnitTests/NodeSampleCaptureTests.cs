using System.Text;
using Beep.KocAiCommunity.Application.ML;
using Beep.KocAiCommunity.Contracts.Workflow;
using Beep.KocAiCommunity.ML.Nodes;
using FluentAssertions;
using Xunit;

namespace Beep.KocAiCommunity.UnitTests;

/// <summary>
/// What the executor keeps about each node, so a pipeline can be inspected instead of deduced.
/// <para>
/// The bound is the point. Retaining whole tables for a forty-node pipeline runs a workstation out of
/// memory and makes the API response enormous, so the sample is bounded where it is built and the
/// record says what it left out.
/// </para>
/// </summary>
[Collection(MlTrainingCollection.Name)]
public class NodeSampleCaptureTests
{
    // Every graph here ends in a cluster node: the compiler requires a model, and clustering is
    // unsupervised, so these exercise sample capture without paying for an AutoML search.
    private static PluginNodeExecutor NewExecutor()
    {
        var handlers = typeof(PluginNodeExecutor).Assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false } && typeof(IPipelineNodeHandler).IsAssignableFrom(t))
            .Select(t => (IPipelineNodeHandler)Activator.CreateInstance(t)!);
        return new PluginNodeExecutor(new PluginNodeRegistry(handlers));
    }

    /// <summary>Rows well past the sample bound, so truncation is exercised rather than assumed.</summary>
    private static MemoryStream WideCsv(int rows, int featureColumns)
    {
        var header = new StringBuilder();
        for (var c = 0; c < featureColumns; c++)
        {
            header.Append($"x{c},");
        }

        var sb = new StringBuilder(header.Append("label\n").ToString());
        for (var i = 0; i < rows; i++)
        {
            var positive = i % 2 == 0;
            for (var c = 0; c < featureColumns; c++)
            {
                sb.Append(positive ? 7 + (c % 3) : c % 3).Append(',');
            }

            sb.Append(positive ? "true\n" : "false\n");
        }

        return new MemoryStream(Encoding.UTF8.GetBytes(sb.ToString()));
    }

    [Fact]
    public async Task A_node_reports_the_rows_that_went_in_and_came_out()
    {
        // The number people come looking for when a metric is wrong: a split that removed far more
        // than expected is invisible in a log line.
        var def = new WorkflowDefinition
        {
            Name = "sampled",
            Nodes =
            [
                new() { Id = "d", Kind = "dataset" },
                new() { Id = "sp", Kind = "split" },
                new() { Id = "cl", Kind = "cluster" },
            ],
            Edges = [new("d", "sp"), new("sp", "cl")],
        };

        using var csv = WideCsv(200, 3);
        var result = await NewExecutor().ExecuteAsync(def, "label", MlTaskType.BinaryClassification, csv, 20);

        var split = result.Nodes.Single(n => n.NodeId == "sp");
        split.RowsIn.Should().Be(200);
        split.RowsOut.Should().BeGreaterThan(0);
        split.ElapsedMs.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task The_sample_is_bounded_and_says_what_it_left_out()
    {
        var def = new WorkflowDefinition
        {
            Name = "wide",
            Nodes = [new() { Id = "d", Kind = "dataset" }, new() { Id = "cl", Kind = "cluster" }],
            Edges = [new("d", "cl")],
        };

        using var csv = WideCsv(NodeSample.MaxRows + 250, NodeSample.MaxColumns + 20);
        var result = await NewExecutor().ExecuteAsync(def, "label", MlTaskType.BinaryClassification, csv, 20);

        var sample = result.Nodes.Single(n => n.NodeId == "d").Sample;
        sample.Should().NotBeNull();
        sample!.Rows.Should().HaveCount(NodeSample.MaxRows);
        sample.Columns.Should().HaveCount(NodeSample.MaxColumns);

        // Without these the view would show the first 50 of 71 columns and invite a conclusion drawn
        // from a fraction of the data.
        sample.RowsTruncated.Should().BeTrue();
        sample.ColumnsTruncated.Should().BeTrue();
        sample.TotalColumns.Should().Be(NodeSample.MaxColumns + 21, "50 features plus the label");
        sample.TotalRows.Should().Be(NodeSample.MaxRows + 250);
    }

    [Fact]
    public async Task A_small_table_is_not_reported_as_truncated()
    {
        var def = new WorkflowDefinition
        {
            Name = "small",
            Nodes = [new() { Id = "d", Kind = "dataset" }, new() { Id = "cl", Kind = "cluster" }],
            Edges = [new("d", "cl")],
        };

        using var csv = WideCsv(6, 3);
        var result = await NewExecutor().ExecuteAsync(def, "label", MlTaskType.BinaryClassification, csv, 20);

        var sample = result.Nodes.Single(n => n.NodeId == "d").Sample!;
        sample.Rows.Should().HaveCount(6);
        sample.RowsTruncated.Should().BeFalse();
        sample.ColumnsTruncated.Should().BeFalse();
        sample.Rows[0].Should().HaveCount(4, "three features and the label");
    }

    [Fact]
    public async Task Every_node_that_ran_carries_its_own_sample()
    {
        // One sample per node is what makes tracing a wrong metric to its step possible at all.
        var def = new WorkflowDefinition
        {
            Name = "chain",
            Nodes =
            [
                new() { Id = "d", Kind = "dataset" },
                new() { Id = "q", Kind = "sql", Config = new Dictionary<string, string> { ["sql"] = "SELECT x0, label FROM working" } },
                new() { Id = "sp", Kind = "split" },
                new() { Id = "cl", Kind = "cluster" },
            ],
            Edges = [new("d", "q"), new("q", "sp"), new("sp", "cl")],
        };

        using var csv = WideCsv(60, 3);
        var result = await NewExecutor().ExecuteAsync(def, "label", MlTaskType.BinaryClassification, csv, 20);

        result.Nodes.Where(n => n.Status != "failed").Should().OnlyContain(n => n.Sample != null);
        result.Nodes.Single(n => n.NodeId == "q").Sample!.Columns
            .Should().BeEquivalentTo(["x0", "label"], "the projection is visible in the sample");
    }

    [Fact]
    public async Task A_failed_node_still_reports_what_went_into_it()
    {
        // The run that fails is the one somebody most wants the numbers for.
        var def = new WorkflowDefinition
        {
            Name = "broken",
            Nodes =
            [
                new() { Id = "d", Kind = "dataset" },
                new() { Id = "q", Kind = "sql", Config = new Dictionary<string, string> { ["sql"] = "SELECT nope FROM working" } },
                new() { Id = "cl", Kind = "cluster" },
            ],
            Edges = [new("d", "q"), new("q", "cl")],
        };

        using var csv = WideCsv(20, 2);
        var result = await NewExecutor().ExecuteAsync(def, "label", MlTaskType.BinaryClassification, csv, 20);

        result.Success.Should().BeFalse();
        result.Nodes.Single(n => n.NodeId == "q").RowsIn.Should().Be(20);
    }
}
