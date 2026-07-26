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
    public void Fan_out_is_rejected_the_executor_threads_a_single_chain()
    {
        // A node feeding two consumers: the linear executor would thread the wrong table into one of them.
        var def = new WorkflowDefinition
        {
            Nodes = [Node("d", "dataset"), Node("n", "normalize"), Node("s", "standardize"), Node("tr", "train")],
            Edges = [new("d", "n"), new("d", "s"), new("n", "tr")],
        };

        var result = WorkflowCompiler.Compile(def);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("'d'") && e.Contains("feeds 2"));
    }

    [Fact]
    public void Fan_in_is_rejected_the_executor_threads_a_single_chain()
    {
        // A node with two producers: it would receive whichever ran last, not a merge.
        var def = new WorkflowDefinition
        {
            Nodes = [Node("d", "dataset"), Node("n", "normalize"), Node("sp", "split"), Node("tr", "train")],
            Edges = [new("d", "sp"), new("n", "sp"), new("sp", "tr")],
        };

        var result = WorkflowCompiler.Compile(def);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("'sp'") && e.Contains("2 inputs"));
    }

    [Fact]
    public void A_duplicate_edge_is_not_mistaken_for_a_branch()
    {
        // The same connection listed twice is still a single chain — must stay valid.
        var def = new WorkflowDefinition
        {
            Nodes = [Node("src", "dataset"), Node("tr", "train")],
            Edges = [new("src", "tr"), new("src", "tr")],
        };

        WorkflowCompiler.Compile(def).IsValid.Should().BeTrue();
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
