using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Beep.KocAiCommunity.Contracts.Competitions;
using FluentAssertions;
using Xunit;

namespace Beep.KocAiCommunity.IntegrationTests;

/// <summary>
/// Making a submission retry safe.
/// <para>
/// Submissions are quota-limited, so a client that resends a request it never saw the answer to is
/// choosing between losing the work and spending a participant's attempt twice. This is the platform
/// half of the desktop's offline queue, and the desktop should not queue anything until it holds.
/// </para>
/// </summary>
public class SubmissionIdempotencyTests(KocApiFactory factory) : IClassFixture<KocApiFactory>
{
    private readonly KocApiFactory _factory = factory;

    private static MultipartFormDataContent CsvFile(string content)
    {
        var form = new MultipartFormDataContent();
        var part = new StringContent(content, Encoding.UTF8);
        part.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        form.Add(part, "file", "predictions.csv");
        return form;
    }

    private async Task<Guid> CompetitionWithQuotaAsync(HttpClient host, string name, int quota)
    {
        var created = await (await host.PostAsJsonAsync("/api/v1/competitions",
                new CreateCompetitionRequest(name, "for idempotency", "Company", null, null, quota, "accuracy")))
            .Content.ReadFromJsonAsync<CompetitionDto>();

        (await host.PostAsync($"/api/v1/competitions/{created!.Id}/answer-key", CsvFile("id,label\n1,A\n2,A\n3,A")))
            .EnsureSuccessStatusCode();

        return created.Id;
    }

    private static async Task<HttpResponseMessage> SubmitAsync(HttpClient client, Guid competitionId, string? key)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post, $"/api/v1/competitions/{competitionId}/submissions")
        {
            Content = CsvFile("id,label\n1,A\n2,A\n3,A"),
        };

        if (key is not null)
        {
            request.Headers.TryAddWithoutValidation("Idempotency-Key", key);
        }

        return await client.SendAsync(request);
    }

    [Fact]
    public async Task Replaying_a_submission_returns_the_first_one_and_does_not_spend_the_quota()
    {
        // The whole point: a quota of one, submitted twice with the same key, must leave the
        // participant with the attempt they actually spent — one.
        var host = _factory.CreateClientAs("idem-host", "Employee");
        var competitionId = await CompetitionWithQuotaAsync(host, $"Idempotent {Guid.NewGuid():N}", quota: 1);
        var competitor = _factory.CreateClientAs("idem-one", "Employee");
        var key = Guid.NewGuid().ToString();

        var first = await (await SubmitAsync(competitor, competitionId, key))
            .Content.ReadFromJsonAsync<SubmissionResultDto>();

        var replay = await SubmitAsync(competitor, competitionId, key);

        replay.StatusCode.Should().Be(HttpStatusCode.OK, "a retry is not a rejection");
        var second = await replay.Content.ReadFromJsonAsync<SubmissionResultDto>();
        second!.SubmissionId.Should().Be(first!.SubmissionId, "it is the same submission, not a new one");
        second.Score.Should().Be(first.Score);

        var mine = await competitor.GetFromJsonAsync<List<SubmissionResultDto>>(
            $"/api/v1/competitions/{competitionId}/submissions");
        mine.Should().ContainSingle("the retry must not have been recorded as a second attempt");
    }

    [Fact]
    public async Task A_different_key_is_a_different_submission()
    {
        // Idempotency must not become deduplication: two genuine attempts are two attempts.
        var host = _factory.CreateClientAs("idem-host2", "Employee");
        var competitionId = await CompetitionWithQuotaAsync(host, $"Distinct {Guid.NewGuid():N}", quota: 5);
        var competitor = _factory.CreateClientAs("idem-two", "Employee");

        (await SubmitAsync(competitor, competitionId, Guid.NewGuid().ToString())).EnsureSuccessStatusCode();
        (await SubmitAsync(competitor, competitionId, Guid.NewGuid().ToString())).EnsureSuccessStatusCode();

        var mine = await competitor.GetFromJsonAsync<List<SubmissionResultDto>>(
            $"/api/v1/competitions/{competitionId}/submissions");
        mine.Should().HaveCount(2);
    }

    [Fact]
    public async Task One_persons_key_is_not_another_persons()
    {
        // Keys are client-generated, so two people can pick the same string. Scoping the uniqueness to
        // the submitter stops one participant's retry from swallowing another's attempt.
        var host = _factory.CreateClientAs("idem-host3", "Employee");
        var competitionId = await CompetitionWithQuotaAsync(host, $"Shared key {Guid.NewGuid():N}", quota: 5);
        const string sameKey = "not-actually-unique";

        (await SubmitAsync(_factory.CreateClientAs("idem-a", "Employee"), competitionId, sameKey))
            .EnsureSuccessStatusCode();
        (await SubmitAsync(_factory.CreateClientAs("idem-b", "Employee"), competitionId, sameKey))
            .EnsureSuccessStatusCode();

        var a = await _factory.CreateClientAs("idem-a", "Employee")
            .GetFromJsonAsync<List<SubmissionResultDto>>($"/api/v1/competitions/{competitionId}/submissions");
        var b = await _factory.CreateClientAs("idem-b", "Employee")
            .GetFromJsonAsync<List<SubmissionResultDto>>($"/api/v1/competitions/{competitionId}/submissions");

        a.Should().ContainSingle();
        b.Should().ContainSingle();
    }

    [Fact]
    public async Task Without_a_key_the_quota_still_bites()
    {
        // The ordinary online path sends no key, and must behave exactly as it did before.
        var host = _factory.CreateClientAs("idem-host4", "Employee");
        var competitionId = await CompetitionWithQuotaAsync(host, $"No key {Guid.NewGuid():N}", quota: 1);
        var competitor = _factory.CreateClientAs("idem-nokey", "Employee");

        (await SubmitAsync(competitor, competitionId, key: null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await SubmitAsync(competitor, competitionId, key: null)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Several_keyless_submissions_can_coexist()
    {
        // The unique index has to skip nulls. If it counted them, exactly one keyless submission could
        // ever exist per person per competition — which would break the ordinary path completely.
        var host = _factory.CreateClientAs("idem-host5", "Employee");
        var competitionId = await CompetitionWithQuotaAsync(host, $"Nulls {Guid.NewGuid():N}", quota: 5);
        var competitor = _factory.CreateClientAs("idem-nulls", "Employee");

        (await SubmitAsync(competitor, competitionId, key: null)).EnsureSuccessStatusCode();
        (await SubmitAsync(competitor, competitionId, key: null)).EnsureSuccessStatusCode();
        (await SubmitAsync(competitor, competitionId, key: null)).EnsureSuccessStatusCode();

        var mine = await competitor.GetFromJsonAsync<List<SubmissionResultDto>>(
            $"/api/v1/competitions/{competitionId}/submissions");
        mine.Should().HaveCount(3);
    }

    [Fact]
    public async Task Concurrent_replays_of_the_same_key_produce_one_submission()
    {
        // What a retry storm looks like: the client resends while the first request is still in flight.
        // The upfront check races, so the transaction re-checks — this is the test for that.
        var host = _factory.CreateClientAs("idem-host6", "Employee");
        var competitionId = await CompetitionWithQuotaAsync(host, $"Race {Guid.NewGuid():N}", quota: 5);
        var competitor = _factory.CreateClientAs("idem-race", "Employee");
        var key = Guid.NewGuid().ToString();

        var responses = await Task.WhenAll(
            Enumerable.Range(0, 4).Select(_ => SubmitAsync(competitor, competitionId, key)));

        responses.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.OK);

        var mine = await competitor.GetFromJsonAsync<List<SubmissionResultDto>>(
            $"/api/v1/competitions/{competitionId}/submissions");
        mine.Should().ContainSingle("four concurrent replays are still one submission");
    }
}
