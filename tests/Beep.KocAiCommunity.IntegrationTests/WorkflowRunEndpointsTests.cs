using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Beep.KocAiCommunity.Contracts.Datasets;
using Beep.KocAiCommunity.Contracts.Jobs;
using Beep.KocAiCommunity.Contracts.Workflow;
using FluentAssertions;
using Xunit;

namespace Beep.KocAiCommunity.IntegrationTests;

public class WorkflowRunEndpointsTests(KocApiFactory factory) : IClassFixture<KocApiFactory>
{
    private readonly KocApiFactory _factory = factory;

    private const string ValidGraph = """
    {"schemaVersion":1,"name":"ESP","nodes":[
      {"id":"ds","kind":"dataset"},{"id":"sp","kind":"split"},
      {"id":"tr","kind":"train"},{"id":"ev","kind":"evaluate"}],
     "edges":[{"fromNodeId":"ds","toNodeId":"sp"},{"fromNodeId":"sp","toNodeId":"tr"},{"fromNodeId":"tr","toNodeId":"ev"}]}
    """;

    private const string Csv = "x1,x2,label\n9,9,true\n0,0,false\n8,7,true\n1,0,false\n";

    [Fact]
    public async Task Running_a_published_workflow_enqueues_a_run_against_a_dataset()
    {
        var me = _factory.CreateClientAs("emp1", "Employee");
        var workflowId = await PublishWorkflowAsync(me);
        var datasetId = await CreateDatasetWithFileAsync(me, "Team", "Internal");

        var enqueued = await (await me.PostAsJsonAsync($"/api/v1/workflows/{workflowId}/versions/1/run",
            new RunWorkflowVersionRequest(datasetId, "label", "BinaryClassification", 5)))
            .Content.ReadFromJsonAsync<RunEnqueuedDto>();
        enqueued!.RunId.Should().NotBeEmpty();

        // The durable run shows up in the queue (the Worker executes it out-of-band).
        var runs = await me.GetFromJsonAsync<List<RunDto>>("/api/v1/runs");
        runs.Should().Contain(r => r.Id == enqueued.RunId && r.Type == "workflow.run");
    }

    [Fact]
    public async Task A_draft_version_cannot_be_run()
    {
        var me = _factory.CreateClientAs("emp1", "Employee");
        var created = await (await me.PostAsJsonAsync("/api/v1/workflows",
            new CreateWorkflowRequest("Draft only", "", "Internal"))).Content.ReadFromJsonAsync<WorkflowSummaryDto>();
        await me.PostAsJsonAsync($"/api/v1/workflows/{created!.Id}/versions", new SaveDraftRequest(ValidGraph, null));
        var datasetId = await CreateDatasetWithFileAsync(me, "Team", "Internal");

        (await me.PostAsJsonAsync($"/api/v1/workflows/{created.Id}/versions/1/run",
            new RunWorkflowVersionRequest(datasetId, "label", "BinaryClassification", 5)))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Running_against_a_confidential_dataset_is_owner_gated()
    {
        var owner = _factory.CreateClientAs("emp1", "Employee");
        var workflowId = await PublishWorkflowAsync(owner);
        var datasetId = await CreateDatasetWithFileAsync(owner, "Company", "Confidential");

        var other = _factory.CreateClientAs("emp2", "Employee");
        (await other.PostAsJsonAsync($"/api/v1/workflows/{workflowId}/versions/1/run",
            new RunWorkflowVersionRequest(datasetId, "label", "BinaryClassification", 5)))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private async Task<Guid> PublishWorkflowAsync(HttpClient client)
    {
        var created = await (await client.PostAsJsonAsync("/api/v1/workflows",
            new CreateWorkflowRequest("Runnable", "", "Internal"))).Content.ReadFromJsonAsync<WorkflowSummaryDto>();
        await client.PostAsJsonAsync($"/api/v1/workflows/{created!.Id}/versions", new SaveDraftRequest(ValidGraph, null));
        (await client.PostAsync($"/api/v1/workflows/{created.Id}/versions/1/publish", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        return created.Id;
    }

    private async Task<Guid> CreateDatasetWithFileAsync(HttpClient client, string scope, string classification)
    {
        var created = await (await client.PostAsJsonAsync("/api/v1/datasets",
            new CreateDatasetRequest("Data", "", scope, classification, "upstream", null))).Content.ReadFromJsonAsync<DatasetDto>();
        var form = new MultipartFormDataContent();
        var part = new StringContent(Csv);
        part.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        form.Add(part, "file", "data.csv");
        (await client.PostAsync($"/api/v1/datasets/{created!.Id}/files", form)).EnsureSuccessStatusCode();
        return created.Id;
    }
}
