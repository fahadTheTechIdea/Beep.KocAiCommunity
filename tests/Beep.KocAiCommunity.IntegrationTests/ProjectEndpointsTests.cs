using System.Net;
using System.Net.Http.Json;
using Beep.KocAiCommunity.Contracts.Studio;
using FluentAssertions;
using Xunit;

namespace Beep.KocAiCommunity.IntegrationTests;

public class ProjectEndpointsTests(KocApiFactory factory) : IClassFixture<KocApiFactory>
{
    private readonly KocApiFactory _factory = factory;

    [Fact]
    public async Task Projects_are_created_saved_and_owner_isolated()
    {
        var user = _factory.CreateClientAs("proj-a", "Employee");

        var created = (await (await user.PostAsJsonAsync("/api/v1/projects", new CreateProjectRequest("My model", null)))
            .Content.ReadFromJsonAsync<ProjectDto>())!;
        created.Name.Should().Be("My model");
        created.CompetitionId.Should().BeNull();

        (await user.GetFromJsonAsync<List<ProjectDto>>("/api/v1/projects"))!
            .Should().ContainSingle(p => p.Id == created.Id);

        // Save a workflow definition + label/task.
        var save = await user.PutAsJsonAsync($"/api/v1/projects/{created.Id}",
            new SaveProjectRequest("{\"name\":\"p\",\"nodes\":[],\"edges\":[]}", "target", "Regression"));
        save.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var detail = (await user.GetFromJsonAsync<ProjectDetailDto>($"/api/v1/projects/{created.Id}"))!;
        detail.LabelColumn.Should().Be("target");
        detail.TaskType.Should().Be("Regression");
        detail.DefinitionJson.Should().Contain("nodes");

        // A different user cannot see or open it.
        var other = _factory.CreateClientAs("proj-b", "Employee");
        (await other.GetFromJsonAsync<List<ProjectDto>>("/api/v1/projects"))!
            .Should().NotContain(p => p.Id == created.Id);
        (await other.GetAsync($"/api/v1/projects/{created.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Delete.
        (await user.DeleteAsync($"/api/v1/projects/{created.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await user.GetFromJsonAsync<List<ProjectDto>>("/api/v1/projects"))!
            .Should().NotContain(p => p.Id == created.Id);
    }
}
