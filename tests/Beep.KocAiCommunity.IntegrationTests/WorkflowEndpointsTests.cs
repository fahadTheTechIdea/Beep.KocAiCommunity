using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Beep.KocAiCommunity.Contracts.Studio;
using Beep.KocAiCommunity.Contracts.Workflow;
using FluentAssertions;
using Xunit;

namespace Beep.KocAiCommunity.IntegrationTests;

public class WorkflowEndpointsTests(KocApiFactory factory) : IClassFixture<KocApiFactory>
{
    private readonly KocApiFactory _factory = factory;

    [Fact]
    public async Task Validate_reports_invalid_when_train_node_is_missing()
    {
        var client = _factory.CreateClientAs("wf-user", "Employee");
        var def = new WorkflowDefinition { Nodes = [new() { Id = "src", Kind = "dataset" }] };

        var result = (await (await client.PostAsJsonAsync("/api/v1/studio/workflows/validate", def))
            .Content.ReadFromJsonAsync<WorkflowValidationResult>())!;

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("train"));
    }

}
