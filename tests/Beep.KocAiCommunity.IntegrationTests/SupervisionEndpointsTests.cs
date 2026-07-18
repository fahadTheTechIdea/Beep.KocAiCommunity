using System.Net;
using System.Net.Http.Json;
using Beep.KocAiCommunity.Contracts.Learning;
using Beep.KocAiCommunity.Contracts.Supervision;
using FluentAssertions;
using Xunit;

namespace Beep.KocAiCommunity.IntegrationTests;

public class SupervisionEndpointsTests(KocApiFactory factory) : IClassFixture<KocApiFactory>
{
    private readonly KocApiFactory _factory = factory;

    [Fact]
    public async Task Employee_cannot_see_supervision_rollup()
    {
        var employee = _factory.CreateClientAs("emp1", "Employee");
        (await employee.GetAsync("/api/v1/supervision/rollup")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Manager_sees_subtree_participation_including_a_members_enrollment()
    {
        // A team member (emp1 is in team t1 ⊂ group g1, which mgr1 leads) enrolls in a track.
        var emp1 = _factory.CreateClientAs("emp1", "Employee");
        var tracks = (await emp1.GetFromJsonAsync<List<TrackDto>>("/api/v1/tracks"))!;
        var trackId = tracks.First().Id;
        (await emp1.PostAsync($"/api/v1/tracks/{trackId}/enroll", null)).EnsureSuccessStatusCode();

        // The manager sees the rollup for their group subtree.
        var manager = _factory.CreateClientAs("mgr1", "Manager");
        var rollup = (await manager.GetFromJsonAsync<SupervisionRollupDto>("/api/v1/supervision/rollup"))!;

        rollup.ScopeLabel.Should().Contain("Group");
        rollup.Members.Should().Contain(m => m.UserId == "emp1");
        rollup.Members.Single(m => m.UserId == "emp1").Enrollments.Should().BeGreaterThanOrEqualTo(1);
        rollup.ActiveLearners.Should().BeGreaterThanOrEqualTo(1);
        rollup.Members.Should().NotContain(m => m.UserId == "empOther"); // empOther is under a different directorate
    }
}
