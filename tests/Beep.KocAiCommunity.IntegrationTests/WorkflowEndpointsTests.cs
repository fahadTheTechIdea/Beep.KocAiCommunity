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

    [Fact]
    public async Task Running_a_valid_workflow_trains_a_model()
    {
        var client = _factory.CreateClientAs("wf-runner", "Employee");

        var def = new WorkflowDefinition
        {
            Name = "ESP failure workflow",
            Nodes = [new() { Id = "src", Kind = "dataset" }, new() { Id = "tr", Kind = "train" }],
            Edges = [new("src", "tr")],
        };

        var sb = new StringBuilder("x1,x2,label\n");
        for (var i = 0; i < 60; i++)
        {
            sb.Append($"{7 + (i % 3)},{7 + ((i / 3) % 3)},true\n");
            sb.Append($"{i % 3},{(i / 3) % 3},false\n");
        }

        using var form = new MultipartFormDataContent
        {
            { new StringContent(JsonSerializer.Serialize(def)), "definition" },
        };
        var csv = new StringContent(sb.ToString());
        csv.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        form.Add(csv, "file", "data.csv");

        var response = await client.PostAsync("/api/v1/studio/workflows/run?labelColumn=label", form);
        response.EnsureSuccessStatusCode();

        var run = (await response.Content.ReadFromJsonAsync<ModelRunDto>())!;
        run.DatasetName.Should().Be("ESP failure workflow");
        run.PrimaryValue.Should().BeInRange(0.0, 1.0);
        run.RowCount.Should().Be(120);
    }
}
