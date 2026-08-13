using System.Net.Http.Headers;
using System.Net.Http.Json;
using Beep.KocAiCommunity.Contracts.Competitions;
using Beep.KocAiCommunity.Contracts.Dashboard;
using FluentAssertions;
using Xunit;

namespace Beep.KocAiCommunity.IntegrationTests;

public class DashboardEndpointsTests(KocApiFactory factory) : IClassFixture<KocApiFactory>
{
    private readonly KocApiFactory _factory = factory;

    [Fact]
    public async Task Personal_dashboard_reflects_competition_activity()
    {
        var creator = _factory.CreateClientAs("dash-creator", "Employee");
        var create = await creator.PostAsJsonAsync("/api/v1/competitions",
            new CreateCompetitionRequest("Dash comp", "predict", "Company", null, null, 5, "accuracy"));
        var competitionId = (await create.Content.ReadFromJsonAsync<CompetitionDto>())!.Id;
        await creator.PostAsync($"/api/v1/competitions/{competitionId}/answer-key", CsvFile("id,label\n1,A\n2,B"));
        (await creator.PostAsJsonAsync($"/api/v1/competitions/{competitionId}/status", new SetStatusRequest("active")))
            .EnsureSuccessStatusCode();

        var competitor = _factory.CreateClientAs("dash-a", "Employee");
        await competitor.PostAsync($"/api/v1/competitions/{competitionId}/submissions", CsvFile("id,label\n1,A\n2,B"));

        var me = (await competitor.GetFromJsonAsync<PersonalDashboardDto>("/api/v1/dashboard/me"))!;
        me.CompetitionsEntered.Should().Be(1);
        me.Submissions.Should().BeGreaterThanOrEqualTo(1);
        me.Standings.Should().ContainSingle(s => s.CompetitionId == competitionId)
            .Which.Rank.Should().Be(1);

        // A user who did nothing has an empty snapshot.
        var idle = (await _factory.CreateClientAs("dash-idle", "Employee").GetFromJsonAsync<PersonalDashboardDto>("/api/v1/dashboard/me"))!;
        idle.CompetitionsEntered.Should().Be(0);
        idle.Standings.Should().BeEmpty();
    }

    private static MultipartFormDataContent CsvFile(string content)
    {
        var form = new MultipartFormDataContent();
        var part = new StringContent(content);
        part.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        form.Add(part, "file", "data.csv");
        return form;
    }
}
