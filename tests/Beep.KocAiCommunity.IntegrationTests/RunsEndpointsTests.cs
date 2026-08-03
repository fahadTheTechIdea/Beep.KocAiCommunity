using System.Net;
using System.Net.Http.Json;
using Beep.KocAiCommunity.Contracts.Jobs;
using FluentAssertions;
using Xunit;

namespace Beep.KocAiCommunity.IntegrationTests;

public class RunsEndpointsTests(KocApiFactory factory) : IClassFixture<KocApiFactory>
{
    private readonly KocApiFactory _factory = factory;

    [Fact]
    public async Task Enqueue_run_returns_a_pending_job_visible_to_its_owner()
    {
        var emp = _factory.CreateClientAs("emp1", "Employee");

        var created = await (await emp.PostAsJsonAsync("/api/v1/runs",
            new CreateRunRequest("report.generate", "Monthly report", "{}")))
            .Content.ReadFromJsonAsync<RunDto>();

        created.Should().NotBeNull();
        created!.Status.Should().Be("pending");

        var fetched = await emp.GetFromJsonAsync<RunDto>($"/api/v1/runs/{created.Id}");
        fetched!.Title.Should().Be("Monthly report");

        var mine = await emp.GetFromJsonAsync<List<RunDto>>("/api/v1/runs");
        mine!.Should().Contain(r => r.Id == created.Id);
    }

    [Fact]
    public async Task A_run_can_be_cancelled_by_its_owner()
    {
        var emp = _factory.CreateClientAs("emp1", "Employee");
        var created = await (await emp.PostAsJsonAsync("/api/v1/runs",
            new CreateRunRequest("report.generate", "Cancel me", "{}")))
            .Content.ReadFromJsonAsync<RunDto>();

        (await emp.PostAsync($"/api/v1/runs/{created!.Id}/cancel", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var after = await emp.GetFromJsonAsync<RunDto>($"/api/v1/runs/{created.Id}");
        after!.Status.Should().Be("cancelled"); // a pending run cancels immediately
    }

    [Fact]
    public async Task Another_employee_cannot_see_someone_elses_run()
    {
        var owner = _factory.CreateClientAs("emp1", "Employee");
        var created = await (await owner.PostAsJsonAsync("/api/v1/runs",
            new CreateRunRequest("report.generate", "Private run", "{}")))
            .Content.ReadFromJsonAsync<RunDto>();

        var other = _factory.CreateClientAs("empOther", "Employee");
        (await other.GetAsync($"/api/v1/runs/{created!.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await other.PostAsync($"/api/v1/runs/{created.Id}/cancel", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Logs_and_attempts_are_available_to_the_owner()
    {
        var emp = _factory.CreateClientAs("emp1", "Employee");
        var created = await (await emp.PostAsJsonAsync("/api/v1/runs",
            new CreateRunRequest("report.generate", "With logs", "{}")))
            .Content.ReadFromJsonAsync<RunDto>();

        (await emp.GetAsync($"/api/v1/runs/{created!.Id}/logs")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await emp.GetAsync($"/api/v1/runs/{created.Id}/attempts")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Runs_require_authentication()
    {
        var anon = _factory.CreateClientAs(null);
        (await anon.GetAsync("/api/v1/runs")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
