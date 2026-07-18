using Beep.KocAiCommunity.Contracts.Workflow;
using Beep.KocAiCommunity.Workflow;
using FluentAssertions;
using Xunit;

namespace Beep.KocAiCommunity.UnitTests;

public class WorkflowCompilerTests
{
    private static WorkflowNode Node(string id, string kind) => new() { Id = id, Kind = kind };

    [Fact]
    public void Valid_workflow_compiles_and_orders_source_before_train()
    {
        var def = new WorkflowDefinition
        {
            Name = "Predict pump failure",
            Nodes = [Node("src", "dataset"), Node("tr", "train"), Node("ev", "evaluate")],
            Edges = [new("src", "tr"), new("tr", "ev")],
        };

        var result = WorkflowCompiler.Compile(def);

        result.IsValid.Should().BeTrue();
        result.Order.Should().ContainInOrder("src", "tr", "ev");
    }

    [Fact]
    public void Missing_train_node_is_invalid()
    {
        var def = new WorkflowDefinition { Nodes = [Node("src", "dataset")] };
        var result = WorkflowCompiler.Compile(def);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("train"));
    }

    [Fact]
    public void A_cycle_is_detected()
    {
        var def = new WorkflowDefinition
        {
            Nodes = [Node("a", "dataset"), Node("b", "train")],
            Edges = [new("a", "b"), new("b", "a")],
        };

        var result = WorkflowCompiler.Compile(def);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("cycle"));
    }

    [Fact]
    public void Unknown_node_kind_is_invalid()
    {
        var def = new WorkflowDefinition
        {
            Nodes = [Node("a", "dataset"), Node("b", "train"), Node("c", "frobnicate")],
            Edges = [new("a", "b")],
        };

        WorkflowCompiler.Compile(def).Errors.Should().Contain(e => e.Contains("frobnicate"));
    }
}
