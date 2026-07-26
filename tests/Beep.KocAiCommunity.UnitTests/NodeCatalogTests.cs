using Beep.KocAiCommunity.Application.ML;
using Beep.KocAiCommunity.ML.Nodes;
using Beep.KocAiCommunity.Workflow;
using FluentAssertions;
using Xunit;

namespace Beep.KocAiCommunity.UnitTests;

public class NodeCatalogTests
{
    private static readonly PluginNodeRegistry Registry = new(
        typeof(PluginNodeExecutor).Assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false } && typeof(IPipelineNodeHandler).IsAssignableFrom(t))
            .Select(t => (IPipelineNodeHandler)System.Activator.CreateInstance(t)!));

    [Fact]
    public void Every_catalog_kind_is_known_to_the_compiler()
    {
        // The backend catalog and the compiler must agree on node kinds, or graphs won't validate.
        foreach (var node in Registry.All)
        {
            WorkflowCompiler.KnownKinds.Should().Contain(node.Kind);
        }
    }

    [Fact]
    public void Registry_and_compiler_known_kinds_match_exactly()
    {
        // Every registered handler has a known kind AND every known kind has a handler — no drift.
        Registry.Kinds.Should().BeEquivalentTo(WorkflowCompiler.KnownKinds);
    }

    [Fact]
    public void Find_resolves_known_and_unknown_kinds()
    {
        Registry.Find("train").Should().NotBeNull();
        Registry.Find("nope").Should().BeNull();
    }

    [Fact]
    public void Validation_flags_missing_required_bad_number_and_bad_option()
    {
        // select-columns requires 'columns'.
        Registry.ValidateParameters("select-columns", new Dictionary<string, string>()).IsValid.Should().BeFalse();

        // 'sample' fraction must be numeric.
        Registry.ValidateParameters("sample", new Dictionary<string, string> { ["fraction"] = "lots" }).IsValid.Should().BeFalse();

        // 'replace-missing' mode must be one of the options.
        Registry.ValidateParameters("replace-missing", new Dictionary<string, string> { ["mode"] = "median" }).IsValid.Should().BeFalse();

        // Valid inputs pass.
        Registry.ValidateParameters("replace-missing", new Dictionary<string, string> { ["mode"] = "mean" }).IsValid.Should().BeTrue();
        Registry.ValidateParameters("select-columns", new Dictionary<string, string> { ["columns"] = "x1,x2" }).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Task_catalog_marks_supported_and_roadmap_tasks()
    {
        MlTaskCatalog.Find("binary")!.Supported.Should().BeTrue();
        MlTaskCatalog.Find("anomaly")!.Supported.Should().BeFalse();
    }

    [Theory]
    [InlineData("train")]
    [InlineData("cross-validate")]
    public void Model_nodes_expose_the_full_hyperparameter_set(string kind)
    {
        // The property panel is descriptor-driven, so the model nodes must declare every knob the executor
        // reads — algorithm + the four hyperparameters — or they're invisible and locked to defaults.
        var declared = Registry.Find(kind)!.Parameters.Select(p => p.Name);
        declared.Should().Contain(new[] { "algorithm", "trees", "leaves", "learningRate", "l2" });

        var algorithm = Registry.Find(kind)!.Parameters.Single(p => p.Name == "algorithm");
        algorithm.Options!.Select(o => o.Value).Should().BeEquivalentTo(new[] { "sdca", "lbfgs", "fasttree", "fastforest", "perceptron", "naivebayes" });
    }
}
