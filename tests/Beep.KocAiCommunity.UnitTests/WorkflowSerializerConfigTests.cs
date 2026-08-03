using Beep.KocAiCommunity.Contracts.Workflow;
using Beep.KocAiCommunity.Workflow;
using FluentAssertions;
using Xunit;

namespace Beep.KocAiCommunity.UnitTests;

/// <summary>
/// A saved workflow must round-trip its node config — especially the Dataset node's `datasetId` and the Train
/// node's target/task — through canonicalize → parse, or reopening a saved workflow loses the user's data
/// source and settings.
/// </summary>
// Serialised with every other AutoML test. Training gets a fixed wall-clock budget, so when
// several of these run at once they starve each other of cores, complete fewer trials, and fail
// on a worse model than the same test produces alone — which reads as flakiness rather than as
// contention. Slower in total, and honest.
[Collection(MlTrainingCollection.Name)]
public class WorkflowSerializerConfigTests
{
    [Fact]
    public void Canonicalize_then_parse_preserves_node_config()
    {
        var datasetId = Guid.NewGuid().ToString();
        var def = new WorkflowDefinition
        {
            Name = "wf",
            Nodes =
            [
                new() { Id = "d", Kind = "dataset", Config = new Dictionary<string, string> { ["datasetId"] = datasetId, ["_x"] = "40", ["_y"] = "60" } },
                new() { Id = "tr", Kind = "train", Config = new Dictionary<string, string> { ["targetColumn"] = "survived", ["task"] = "BinaryClassification", ["algorithm"] = "fasttree" } },
            ],
            Edges = [new("d", "tr")],
        };

        var canonical = WorkflowSerializer.Canonicalize(def);
        var round = WorkflowSerializer.Parse(canonical);

        var dataset = round.Nodes.Single(n => n.Kind == "dataset");
        dataset.Config.Should().NotBeNull();
        dataset.Config!["datasetId"].Should().Be(datasetId);

        var train = round.Nodes.Single(n => n.Kind == "train");
        train.Config!["targetColumn"].Should().Be("survived");
        train.Config!["task"].Should().Be("BinaryClassification");
        train.Config!["algorithm"].Should().Be("fasttree");
    }
}
