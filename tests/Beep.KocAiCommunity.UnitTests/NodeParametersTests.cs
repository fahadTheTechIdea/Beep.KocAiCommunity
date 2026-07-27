using Beep.KocAiCommunity.Application.ML;
using FluentAssertions;
using Xunit;

namespace Beep.KocAiCommunity.UnitTests;

/// <summary>
/// The typed per-node parameter classes: each node declares its own strongly-typed fields, and the shared
/// base turns them into the panel descriptor and bridges the node's string config to/from the typed values.
/// </summary>
public class NodeParametersTests
{
    [Fact]
    public void Each_node_declares_its_own_distinct_typed_fields()
    {
        // train and binning are different nodes → different field sets and types.
        var train = new TrainParameters();
        train.Describe().Select(p => p.Name).Should().Equal(
            "targetColumn", "idColumn", "featureColumns", "task", "algorithm", "trees", "leaves", "minLeaf", "learningRate", "iterations", "l1", "l2", "maxIterations", "historySize");
        train.Algorithm.Type.Should().Be(NodeParameterType.Select);
        train.Algorithm.Options!.Select(o => o.Value).Should().Contain("fasttree");
        train.Trees.Type.Should().Be(NodeParameterType.Integer);
        train.LearningRate.Type.Should().Be(NodeParameterType.Number);
        // FastTree-only knobs are gated to the tree algorithms.
        train.Trees.VisibleWhen!.Values.Should().Contain("fasttree");

        var binning = new BinningParameters();
        binning.Describe().Select(p => p.Name).Should().Equal("bins", "columns");
        binning.Bins.Min.Should().Be(2);
        binning.Bins.Max.Should().Be(255);
    }

    [Fact]
    public void Load_reads_config_into_typed_fields_and_falls_back_to_defaults()
    {
        var p = new TrainParameters();
        p.Load(new Dictionary<string, string> { ["algorithm"] = "fasttree", ["trees"] = "250" });

        p.Algorithm.Effective.Should().Be("fasttree");
        p.Trees.Get().Should().Be(250);        // from config
        p.Leaves.Get().Should().Be(20);        // default (not in config)
        p.MinLeaf.Get().Should().Be(10);       // default
        p.LearningRate.Get().Should().BeNull(); // blank → each algorithm applies its own default
        p.L2.Get().Should().BeNull();          // no default → unset
    }

    [Fact]
    public void Save_writes_only_set_fields_back_to_config()
    {
        var p = new FilterRowsParameters();
        p.Column.Value = "pressure";
        p.Min.Value = "10";
        // Max left unset → omitted.

        var config = p.Save();

        config.Should().ContainKey("column").WhoseValue.Should().Be("pressure");
        config.Should().ContainKey("min").WhoseValue.Should().Be("10");
        config.Should().NotContainKey("max");
    }

    [Fact]
    public void Describe_projects_bounds_and_required_to_the_panel_contract()
    {
        var sql = new SqlParameters().Describe().Single();
        sql.Required.Should().BeTrue();
        sql.Type.Should().Be(NodeParameterType.Text);

        var fraction = new SampleParameters().Describe().Single();
        fraction.Min.Should().Be(0);
        fraction.Max.Should().Be(1);
        fraction.Default.Should().Be("0.5");
    }
}
