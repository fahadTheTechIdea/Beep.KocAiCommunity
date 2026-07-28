using System.Net;
using System.Net.Http.Json;
using Beep.KocAiCommunity.Contracts.ML;
using Beep.KocAiCommunity.Contracts.Workflow;
using FluentAssertions;
using Xunit;

namespace Beep.KocAiCommunity.IntegrationTests;

public class MlNodeEndpointsTests(KocApiFactory factory) : IClassFixture<KocApiFactory>
{
    private readonly KocApiFactory _factory = factory;

    [Fact]
    public async Task Node_catalog_lists_descriptors_and_validates_parameters()
    {
        var me = _factory.CreateClientAs("ml-user", "Employee");

        var nodes = await me.GetFromJsonAsync<List<NodeDescriptorDto>>("/api/v1/ml/nodes");
        nodes.Should().Contain(n => n.Kind == "train").And.Contain(n => n.Kind == "split");

        var train = await me.GetFromJsonAsync<NodeDescriptorDto>("/api/v1/ml/nodes/train");
        train!.Parameters.Should().Contain(p => p.Name == "algorithm");

        // A missing required parameter fails validation.
        var bad = await (await me.PostAsJsonAsync("/api/v1/ml/nodes/select-columns/validate", new Dictionary<string, string>()))
            .Content.ReadFromJsonAsync<ParameterValidationDto>();
        bad!.IsValid.Should().BeFalse();

        var ok = await (await me.PostAsJsonAsync("/api/v1/ml/nodes/replace-missing/validate", new Dictionary<string, string> { ["mode"] = "mean" }))
            .Content.ReadFromJsonAsync<ParameterValidationDto>();
        ok!.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Task_catalog_and_featurization_check_are_served()
    {
        var me = _factory.CreateClientAs("ml-user2", "Employee");

        var tasks = await me.GetFromJsonAsync<List<MlTaskDto>>("/api/v1/ml/tasks");
        tasks.Should().Contain(t => t.Key == "binary" && t.Supported);
        tasks.Should().Contain(t => t.Key == "anomaly" && t.Supported);
        tasks.Should().OnlyContain(t => t.Supported); // all five tasks are now executable

        // A graph that fits without a split is flagged.
        var leak = new WorkflowDefinition
        {
            Nodes = [new WorkflowNode { Id = "ds", Kind = "dataset" }, new WorkflowNode { Id = "tr", Kind = "train" }],
            Edges = [new WorkflowEdge("ds", "tr")],
        };
        var check = await (await me.PostAsJsonAsync("/api/v1/ml/workflows/featurization-check", leak))
            .Content.ReadFromJsonAsync<FeaturizationCheckDto>();
        check!.Ok.Should().BeFalse();
        check.Violations.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Publishing_a_workflow_that_fits_without_a_split_is_rejected()
    {
        var me = _factory.CreateClientAs("ml-wf", "Employee");
        var created = await (await me.PostAsJsonAsync("/api/v1/workflows",
            new CreateWorkflowRequest("No split", "", "Internal"))).Content.ReadFromJsonAsync<WorkflowSummaryDto>();

        // Compiles (has a dataset + a train), but leaks: no split before the model.
        const string noSplit = """{"schemaVersion":1,"name":"x","nodes":[{"id":"ds","kind":"dataset"},{"id":"tr","kind":"train"}],"edges":[{"fromNodeId":"ds","toNodeId":"tr"}]}""";
        await me.PostAsJsonAsync($"/api/v1/workflows/{created!.Id}/versions", new SaveDraftRequest(noSplit, null));

        var publish = await me.PostAsync($"/api/v1/workflows/{created.Id}/versions/1/publish", null);
        publish.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await publish.Content.ReadAsStringAsync()).Should().Contain("split");
    }
}
