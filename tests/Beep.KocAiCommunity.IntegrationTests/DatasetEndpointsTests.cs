using System.Net;
using System.Net.Http.Json;
using Beep.KocAiCommunity.Contracts.Datasets;
using FluentAssertions;
using Xunit;

namespace Beep.KocAiCommunity.IntegrationTests;

public class DatasetEndpointsTests(KocApiFactory factory) : IClassFixture<KocApiFactory>
{
    private readonly KocApiFactory _factory = factory;

    [Fact]
    public async Task Group_scoped_dataset_is_visible_within_the_group_only()
    {
        // mgr1's home unit is the Subsurface group; a Group-scoped dataset is visible to that subtree.
        var manager = _factory.CreateClientAs("mgr1", "Manager");
        var created = await (await manager.PostAsJsonAsync("/api/v1/datasets",
            new CreateDatasetRequest("Well logs (group)", "PPDM logs", "Group", "Confidential", "upstream", null)))
            .Content.ReadFromJsonAsync<DatasetDto>();
        created!.Scope.Should().Be("Group");

        var insider = _factory.CreateClientAs("emp1", "Employee");     // emp1 ∈ team t1 ⊂ group
        var insiderList = (await insider.GetFromJsonAsync<List<DatasetDto>>("/api/v1/datasets"))!;
        insiderList.Should().Contain(d => d.Id == created.Id);

        var outsider = _factory.CreateClientAs("empOther", "Employee"); // empOther ∈ a different directorate
        var outsiderList = (await outsider.GetFromJsonAsync<List<DatasetDto>>("/api/v1/datasets"))!;
        outsiderList.Should().NotContain(d => d.Id == created.Id);
        (await outsider.GetAsync($"/api/v1/datasets/{created.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Company_scoped_dataset_is_visible_to_everyone()
    {
        var manager = _factory.CreateClientAs("mgr1", "Manager");
        var created = await (await manager.PostAsJsonAsync("/api/v1/datasets",
            new CreateDatasetRequest("Public reference", "Company-wide", "Company", "Internal", "upstream", null)))
            .Content.ReadFromJsonAsync<DatasetDto>();

        var outsider = _factory.CreateClientAs("empOther", "Employee");
        var list = (await outsider.GetFromJsonAsync<List<DatasetDto>>("/api/v1/datasets"))!;
        list.Should().Contain(d => d.Id == created!.Id);
    }

    [Fact]
    public async Task Audience_preview_reports_the_scope_unit_and_count()
    {
        var manager = _factory.CreateClientAs("mgr1", "Manager");

        var group = (await manager.GetFromJsonAsync<VisibilityOptionDto>("/api/v1/me/audience?scope=group"))!;
        group.OrgUnitName.Should().Be("Subsurface");
        group.UserCount.Should().Be(3); // emp1, emp2, mgr1 have memberships under the group

        var company = (await manager.GetFromJsonAsync<VisibilityOptionDto>("/api/v1/me/audience?scope=company"))!;
        company.OrgUnitName.Should().Be("All KOC");
        company.UserCount.Should().Be(4); // + empOther
    }
}
