using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Beep.KocAiCommunity.Contracts.Competitions;
using Beep.KocAiCommunity.Contracts.Notifications;
using FluentAssertions;
using Xunit;

namespace Beep.KocAiCommunity.IntegrationTests;

public class NotificationEndpointsTests(KocApiFactory factory) : IClassFixture<KocApiFactory>
{
    private readonly KocApiFactory _factory = factory;

    [Fact]
    public async Task Scoring_a_submission_notifies_the_submitter()
    {
        var creator = _factory.CreateClientAs("notif-creator", "Employee");
        var create = await creator.PostAsJsonAsync("/api/v1/competitions",
            new CreateCompetitionRequest("Notif comp", "predict", "Company", null, null, 5, "accuracy"));
        var competitionId = (await create.Content.ReadFromJsonAsync<CompetitionDto>())!.Id;
        await creator.PostAsync($"/api/v1/competitions/{competitionId}/answer-key", CsvFile("id,label\n1,A\n2,B"));

        var competitor = _factory.CreateClientAs("notif-a", "Employee");
        (await competitor.PostAsync($"/api/v1/competitions/{competitionId}/submissions", CsvFile("id,label\n1,A\n2,B")))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        // The submitter now has an unread "submission-scored" notification.
        var unread = await competitor.GetFromJsonAsync<CountResponse>("/api/v1/notifications/unread-count");
        unread!.Count.Should().BeGreaterThanOrEqualTo(1);

        var items = (await competitor.GetFromJsonAsync<List<NotificationDto>>("/api/v1/notifications"))!;
        var scored = items.Should().ContainSingle(n => n.Type == "submission-scored").Subject;
        scored.IsRead.Should().BeFalse();

        // The creator (who didn't submit) has none.
        (await creator.GetFromJsonAsync<CountResponse>("/api/v1/notifications/unread-count"))!.Count.Should().Be(0);

        // Mark read → unread count drops.
        (await competitor.PostAsync($"/api/v1/notifications/{scored.Id}/read", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await competitor.GetFromJsonAsync<CountResponse>("/api/v1/notifications/unread-count"))!.Count
            .Should().Be(unread.Count - 1);
    }

    [Fact]
    public async Task Concluding_notifies_participants()
    {
        var creator = _factory.CreateClientAs("notif-owner", "Employee");
        var create = await creator.PostAsJsonAsync("/api/v1/competitions",
            new CreateCompetitionRequest("Wrap up", "predict", "Company", null, null, 5, "accuracy"));
        var competitionId = (await create.Content.ReadFromJsonAsync<CompetitionDto>())!.Id;
        await creator.PostAsync($"/api/v1/competitions/{competitionId}/answer-key", CsvFile("id,label\n1,A"));

        var competitor = _factory.CreateClientAs("notif-p", "Employee");
        await competitor.PostAsync($"/api/v1/competitions/{competitionId}/submissions", CsvFile("id,label\n1,A"));

        (await creator.PostAsJsonAsync($"/api/v1/competitions/{competitionId}/status", new SetStatusRequest("concluded")))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var items = (await competitor.GetFromJsonAsync<List<NotificationDto>>("/api/v1/notifications"))!;
        items.Should().Contain(n => n.Type == "competition-concluded");
    }

    private sealed record CountResponse(int Count);

    private static MultipartFormDataContent CsvFile(string content)
    {
        var form = new MultipartFormDataContent();
        var part = new StringContent(content);
        part.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        form.Add(part, "file", "data.csv");
        return form;
    }
}
