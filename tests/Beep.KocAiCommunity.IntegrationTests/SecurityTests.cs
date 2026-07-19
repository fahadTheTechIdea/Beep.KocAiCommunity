using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Beep.KocAiCommunity.Contracts.Datasets;
using Beep.KocAiCommunity.Contracts.Workflow;
using FluentAssertions;
using Xunit;

namespace Beep.KocAiCommunity.IntegrationTests;

/// <summary>
/// OWASP-aligned security checks across the platform: authentication, broken object-level
/// authorization (IDOR), privilege escalation, and input-handling (path traversal). Provider-specific
/// SSRF and secret-exposure checks live in their own suites.
/// </summary>
public class SecurityTests(KocApiFactory factory) : IClassFixture<KocApiFactory>
{
    private readonly KocApiFactory _factory = factory;

    [Theory]
    [InlineData("/api/v1/me")]
    [InlineData("/api/v1/datasets")]
    [InlineData("/api/v1/workflows")]
    [InlineData("/api/v1/ml/nodes")]
    public async Task Protected_endpoints_reject_anonymous_requests(string url)
    {
        _factory.CreateClientAs(null); // ensure seeding without acting as anyone
        var anon = _factory.CreateClient();
        (await anon.GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_team_scoped_dataset_is_invisible_across_the_org_tree()
    {
        var owner = _factory.CreateClientAs("emp1", "Employee");   // Team t1
        var created = await (await owner.PostAsJsonAsync("/api/v1/datasets",
            new CreateDatasetRequest("Sensitive logs", "", "Team", "Internal", "upstream", null)))
            .Content.ReadFromJsonAsync<DatasetDto>();

        // A peer in a different directorate cannot see it (IDOR / broken object-level authz).
        var outsider = _factory.CreateClientAs("empOther", "Employee");   // Team t9, directorate d2
        (await outsider.GetAsync($"/api/v1/datasets/{created!.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await outsider.GetFromJsonAsync<List<DatasetDto>>("/api/v1/datasets"))!
            .Should().NotContain(d => d.Id == created.Id);
    }

    [Fact]
    public async Task A_non_owner_cannot_mutate_another_users_workflow()
    {
        var owner = _factory.CreateClientAs("emp1", "Employee");
        var wf = await (await owner.PostAsJsonAsync("/api/v1/workflows",
            new CreateWorkflowRequest("Mine", "", "Internal"))).Content.ReadFromJsonAsync<WorkflowSummaryDto>();

        var attacker = _factory.CreateClientAs("emp2", "Employee");
        // Cannot edit, publish, or delete a workflow they don't own (and aren't admin for).
        (await attacker.PostAsJsonAsync($"/api/v1/workflows/{wf!.Id}/versions", new SaveDraftRequest("{}", null)))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await attacker.PostAsync($"/api/v1/workflows/{wf.Id}/versions/1/publish", null))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await attacker.DeleteAsync($"/api/v1/workflows/{wf.Id}"))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Uploaded_file_names_are_stripped_of_path_traversal()
    {
        var owner = _factory.CreateClientAs("emp1", "Employee");
        var ds = await (await owner.PostAsJsonAsync("/api/v1/datasets",
            new CreateDatasetRequest("Traversal", "", "Team", "Internal", "upstream", null)))
            .Content.ReadFromJsonAsync<DatasetDto>();

        var form = new MultipartFormDataContent();
        var part = new StringContent("a,b\n1,2\n");
        part.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        form.Add(part, "file", "../../../etc/passwd.csv");
        await owner.PostAsync($"/api/v1/datasets/{ds!.Id}/files", form);

        var detail = await owner.GetFromJsonAsync<DatasetVersionDetailDto>($"/api/v1/datasets/{ds.Id}/versions/1");
        var path = detail!.Files.Single().LogicalPath;
        path.Should().Be("passwd.csv");
        path.Should().NotContain("..").And.NotContain("/");
    }
}
