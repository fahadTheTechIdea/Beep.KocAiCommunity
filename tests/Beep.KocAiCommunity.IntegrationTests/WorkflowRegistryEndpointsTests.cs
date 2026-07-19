using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Beep.KocAiCommunity.Contracts.Workflow;
using Beep.KocAiCommunity.Infrastructure.Persistence;
using Beep.KocAiCommunity.Infrastructure.Workflow;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Beep.KocAiCommunity.IntegrationTests;

public class WorkflowRegistryEndpointsTests(KocApiFactory factory) : IClassFixture<KocApiFactory>
{
    private readonly KocApiFactory _factory = factory;

    // A valid graph: dataset → split → train → evaluate.
    private const string ValidGraph = """
    {"schemaVersion":1,"name":"ESP","nodes":[
      {"id":"ds","kind":"dataset"},{"id":"sp","kind":"split"},
      {"id":"tr","kind":"train"},{"id":"ev","kind":"evaluate"}],
     "edges":[{"fromNodeId":"ds","toNodeId":"sp"},{"fromNodeId":"sp","toNodeId":"tr"},{"fromNodeId":"tr","toNodeId":"ev"}]}
    """;

    [Fact]
    public async Task Publish_freezes_a_version_and_new_edits_open_a_new_draft()
    {
        var me = _factory.CreateClientAs("wf-owner", "Employee");
        var created = await (await me.PostAsJsonAsync("/api/v1/workflows",
            new CreateWorkflowRequest("Pump model", "desc", "Internal"))).Content.ReadFromJsonAsync<WorkflowSummaryDto>();

        // Save the graph into the initial draft (v1), then publish it.
        await me.PostAsJsonAsync($"/api/v1/workflows/{created!.Id}/versions", new SaveDraftRequest(ValidGraph, "graph"));
        var published = await (await me.PostAsync($"/api/v1/workflows/{created.Id}/versions/1/publish", null))
            .Content.ReadFromJsonAsync<WorkflowVersionDto>();
        published!.Status.Should().Be("published");
        var frozenHash = published.SnapshotHash;

        // Editing again opens v2 (draft) and leaves v1 untouched.
        var v2 = await (await me.PostAsJsonAsync($"/api/v1/workflows/{created.Id}/versions", new SaveDraftRequest(ValidGraph, "tweak")))
            .Content.ReadFromJsonAsync<WorkflowVersionDto>();
        v2!.VersionNumber.Should().Be(2);
        v2.Status.Should().Be("draft");

        var v1 = await me.GetFromJsonAsync<WorkflowVersionDetailDto>($"/api/v1/workflows/{created.Id}/versions/1");
        v1!.Status.Should().Be("published");
        v1.SnapshotHash.Should().Be(frozenHash);   // immutable

        // A published version cannot be re-published.
        (await me.PostAsync($"/api/v1/workflows/{created.Id}/versions/1/publish", null))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Publishing_an_invalid_graph_is_rejected()
    {
        var me = _factory.CreateClientAs("wf-invalid", "Employee");
        var created = await (await me.PostAsJsonAsync("/api/v1/workflows",
            new CreateWorkflowRequest("Bad", "", "Internal"))).Content.ReadFromJsonAsync<WorkflowSummaryDto>();

        // A dataset with no train/cluster node does not compile.
        const string noModel = """{"schemaVersion":1,"name":"x","nodes":[{"id":"ds","kind":"dataset"}],"edges":[]}""";
        await me.PostAsJsonAsync($"/api/v1/workflows/{created!.Id}/versions", new SaveDraftRequest(noModel, null));

        (await me.PostAsync($"/api/v1/workflows/{created.Id}/versions/1/publish", null))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // …and it stays a draft.
        var v1 = await me.GetFromJsonAsync<WorkflowVersionDetailDto>($"/api/v1/workflows/{created.Id}/versions/1");
        v1!.Status.Should().Be("draft");
    }

    [Fact]
    public async Task Export_then_import_reproduces_the_graph()
    {
        var me = _factory.CreateClientAs("wf-export", "Employee");
        var created = await (await me.PostAsJsonAsync("/api/v1/workflows",
            new CreateWorkflowRequest("Exportable", "", "Internal"))).Content.ReadFromJsonAsync<WorkflowSummaryDto>();
        await me.PostAsJsonAsync($"/api/v1/workflows/{created!.Id}/versions", new SaveDraftRequest(ValidGraph, null));

        var export = await (await me.GetAsync($"/api/v1/workflows/{created.Id}/versions/1/export"))
            .Content.ReadFromJsonAsync<WorkflowExportDto>();
        export!.EnvelopeJson.Should().Contain("koc-workflow-export");

        var imported = await (await me.PostAsJsonAsync("/api/v1/workflows/import",
            new ImportWorkflowRequest("Imported copy", export.EnvelopeJson))).Content.ReadFromJsonAsync<WorkflowSummaryDto>();
        imported!.Name.Should().Be("Imported copy");

        // The imported draft's definition matches the source (same canonical graph → same hash).
        var source = await me.GetFromJsonAsync<WorkflowVersionDetailDto>($"/api/v1/workflows/{created.Id}/versions/1");
        var copy = await me.GetFromJsonAsync<WorkflowVersionDetailDto>($"/api/v1/workflows/{imported.Id}/versions/1");
        copy!.SnapshotHash.Should().Be(source!.SnapshotHash);
    }

    [Fact]
    public async Task Templates_list_and_instantiate_into_a_new_draft()
    {
        await SeedTemplatesAsync();
        var me = _factory.CreateClientAs("wf-templates", "Employee");

        var templates = await me.GetFromJsonAsync<List<WorkflowTemplateDto>>("/api/v1/workflow-templates");
        templates.Should().Contain(t => t.Code == "esp-failure-classifier");

        // The O&G subdomain taxonomy filters templates (upstream/midstream/downstream/hse).
        var hse = await me.GetFromJsonAsync<List<WorkflowTemplateDto>>("/api/v1/workflow-templates?domain=hse");
        hse.Should().ContainSingle(t => t.Code == "hse-incident-classifier");

        var wf = await (await me.PostAsJsonAsync("/api/v1/workflow-templates/esp-failure-classifier/instantiate",
            new InstantiateTemplateRequest("My ESP workflow"))).Content.ReadFromJsonAsync<WorkflowSummaryDto>();
        wf!.Name.Should().Be("My ESP workflow");

        // The instantiated draft compiles (dataset → split → train → evaluate).
        var validation = await (await me.PostAsync($"/api/v1/workflows/{wf.Id}/versions/1/validate", null))
            .Content.ReadFromJsonAsync<WorkflowValidationResult>();
        validation!.IsValid.Should().BeTrue();
    }

    private async Task SeedTemplatesAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KocDbContext>();
        await WorkflowTemplateSeeder.SeedAsync(db);
    }
}
