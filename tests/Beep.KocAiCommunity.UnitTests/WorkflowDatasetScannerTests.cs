using Beep.KocAiCommunity.Application.Workflow;
using Beep.KocAiCommunity.Contracts.Workflow;
using FluentAssertions;
using Xunit;

namespace Beep.KocAiCommunity.UnitTests;

/// <summary>
/// The scanner distinguishes the pipeline's <b>primary</b> data source (the `dataset` node) from the
/// <b>secondary</b> datasets that join/union nodes bring in. The primary is opened as the training stream by
/// the caller, so it must NOT be resolved as a secondary (which would double-load it and, on a submit that
/// passes no secondaries, throw).
/// </summary>
// Serialised with every other AutoML test. Training gets a fixed wall-clock budget, so when
// several of these run at once they starve each other of cores, complete fewer trials, and fail
// on a worse model than the same test produces alone — which reads as flakiness rather than as
// contention. Slower in total, and honest.
[Collection(MlTrainingCollection.Name)]
public class WorkflowDatasetScannerTests
{
    private static readonly Guid Primary = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Joined = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void Scanner_finds_only_join_union_secondaries_and_ignores_the_primary_dataset_node()
    {
        var def = new WorkflowDefinition
        {
            Name = "x",
            Nodes =
            [
                new() { Id = "d", Kind = "dataset", Config = new Dictionary<string, string> { ["datasetId"] = Primary.ToString() } },
                new() { Id = "j", Kind = "join-dataset", Config = new Dictionary<string, string> { ["datasetId"] = Joined.ToString() } },
                new() { Id = "tr", Kind = "train" },
            ],
            Edges = [],
        };

        WorkflowDatasetScanner.ReferencedDatasetIds(def).Should().Equal(Joined);
        WorkflowDatasetScanner.PrimaryDatasetId(def).Should().Be(Primary);
    }
}
