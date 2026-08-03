using System.Text.Json;
using Beep.KocAiCommunity.Contracts.Workflow;
using Beep.KocAiCommunity.Workflow;
using FluentAssertions;
using Xunit;

namespace Beep.KocAiCommunity.UnitTests;

// Serialised with every other AutoML test. Training gets a fixed wall-clock budget, so when
// several of these run at once they starve each other of cores, complete fewer trials, and fail
// on a worse model than the same test produces alone — which reads as flakiness rather than as
// contention. Slower in total, and honest.
[Collection(MlTrainingCollection.Name)]
public class WorkflowSerializerTests
{
    private static WorkflowDefinition Sample() => new()
    {
        SchemaVersion = 1,
        Name = "ESP",
        Nodes =
        [
            new WorkflowNode { Id = "ds", Kind = "dataset" },
            new WorkflowNode { Id = "sp", Kind = "split", Config = new Dictionary<string, string> { ["testFraction"] = "0.25" } },
            new WorkflowNode { Id = "tr", Kind = "train" },
            new WorkflowNode { Id = "ev", Kind = "evaluate" },
        ],
        Edges = [new WorkflowEdge("ds", "sp"), new WorkflowEdge("sp", "tr"), new WorkflowEdge("tr", "ev")],
    };

    [Fact]
    public void Canonicalize_round_trips_without_data_loss()
    {
        var canonical = WorkflowSerializer.Canonicalize(Sample());
        var reparsed = WorkflowSerializer.Parse(canonical);

        reparsed.Name.Should().Be("ESP");
        reparsed.Nodes.Should().HaveCount(4);
        reparsed.Edges.Should().HaveCount(3);
        // Canonicalizing again yields the identical bytes.
        WorkflowSerializer.Canonicalize(reparsed).Should().Be(canonical);
    }

    [Fact]
    public void Hash_is_stable_across_equivalent_json_formatting()
    {
        var def = Sample();
        var compact = JsonSerializer.Serialize(def);
        var pretty = JsonSerializer.Serialize(def, new JsonSerializerOptions { WriteIndented = true });

        // Different formatting, same content → same snapshot hash.
        WorkflowSerializer.Hash(compact).Should().Be(WorkflowSerializer.Hash(pretty));
        WorkflowSerializer.Hash(compact).Should().HaveLength(64); // SHA-256 hex
    }

    [Fact]
    public void Parse_rejects_invalid_json()
    {
        var act = () => WorkflowSerializer.Parse("{ not valid");
        act.Should().Throw<WorkflowSerializationException>();
    }

    [Fact]
    public void Compiler_rejects_a_cycle()
    {
        var cyclic = Sample() with
        {
            Edges = [.. Sample().Edges, new WorkflowEdge("ev", "ds")],  // ev → ds closes a loop
        };
        WorkflowCompiler.Compile(cyclic).IsValid.Should().BeFalse();
        WorkflowCompiler.Compile(Sample()).IsValid.Should().BeTrue();
    }
}
