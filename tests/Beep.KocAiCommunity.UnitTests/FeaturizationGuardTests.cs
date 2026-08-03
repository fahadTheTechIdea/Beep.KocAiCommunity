using Beep.KocAiCommunity.Application.ML;
using Beep.KocAiCommunity.Contracts.Workflow;
using FluentAssertions;
using Xunit;

namespace Beep.KocAiCommunity.UnitTests;

// Serialised with every other AutoML test. Training gets a fixed wall-clock budget, so when
// several of these run at once they starve each other of cores, complete fewer trials, and fail
// on a worse model than the same test produces alone — which reads as flakiness rather than as
// contention. Slower in total, and honest.
[Collection(MlTrainingCollection.Name)]
public class FeaturizationGuardTests
{
    private static WorkflowNode Node(string id, string kind) => new() { Id = id, Kind = kind };

    private static WorkflowDefinition Graph(IEnumerable<WorkflowNode> nodes, params (string From, string To)[] edges) => new()
    {
        Nodes = nodes.ToList(),
        Edges = edges.Select(e => new WorkflowEdge(e.From, e.To)).ToList(),
    };

    [Fact]
    public void Fitting_without_a_split_is_flagged()
    {
        var def = Graph([Node("ds", "dataset"), Node("tr", "train")], ("ds", "tr"));
        FeaturizationGuard.Check(def).Should().ContainSingle().Which.Should().Contain("tr");
    }

    [Fact]
    public void A_split_before_the_model_is_clean()
    {
        var def = Graph([Node("ds", "dataset"), Node("sp", "split"), Node("tr", "train")], ("ds", "sp"), ("sp", "tr"));
        FeaturizationGuard.Check(def).Should().BeEmpty();
    }

    [Fact]
    public void A_transform_before_the_split_still_needs_the_split_before_the_model()
    {
        // dataset → normalize → train, no split → leak.
        var leak = Graph([Node("ds", "dataset"), Node("nm", "normalize"), Node("tr", "train")], ("ds", "nm"), ("nm", "tr"));
        FeaturizationGuard.Check(leak).Should().NotBeEmpty();

        // dataset → normalize → split → train → clean.
        var clean = Graph([Node("ds", "dataset"), Node("nm", "normalize"), Node("sp", "split"), Node("tr", "train")],
            ("ds", "nm"), ("nm", "sp"), ("sp", "tr"));
        FeaturizationGuard.Check(clean).Should().BeEmpty();
    }

    [Fact]
    public void Unsupervised_clustering_does_not_require_a_split()
    {
        var def = Graph([Node("ds", "dataset"), Node("cl", "cluster")], ("ds", "cl"));
        FeaturizationGuard.Check(def).Should().BeEmpty();
    }

    [Fact]
    public void A_chronological_time_split_satisfies_the_rule()
    {
        // Forecasting uses time-split instead of a random split; it must count as a split.
        var def = Graph([Node("ds", "dataset"), Node("ts", "time-split"), Node("tr", "train")], ("ds", "ts"), ("ts", "tr"));
        FeaturizationGuard.Check(def).Should().BeEmpty();
    }

    [Fact]
    public void Unsupervised_anomaly_training_does_not_require_a_split()
    {
        // A train node configured for anomaly detection learns "normal" from all rows — no leak, no split needed.
        var train = new WorkflowNode { Id = "tr", Kind = "train", Config = new Dictionary<string, string> { ["task"] = "AnomalyDetection" } };
        var def = Graph([Node("ds", "dataset"), train], ("ds", "tr"));
        FeaturizationGuard.Check(def).Should().BeEmpty();

        // But a supervised train (no task, or a different task) with no split is still a leak.
        var supervised = Graph([Node("ds", "dataset"), Node("tr", "train")], ("ds", "tr"));
        FeaturizationGuard.Check(supervised).Should().NotBeEmpty();
    }
}
