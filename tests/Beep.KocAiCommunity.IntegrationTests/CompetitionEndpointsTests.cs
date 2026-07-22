using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Beep.KocAiCommunity.Contracts.Competitions;
using Beep.KocAiCommunity.Contracts.Workflow;
using Beep.KocAiCommunity.Domain.Organization;
using FluentAssertions;
using Xunit;

namespace Beep.KocAiCommunity.IntegrationTests;

public class CompetitionEndpointsTests(KocApiFactory factory) : IClassFixture<KocApiFactory>
{
    private readonly KocApiFactory _factory = factory;

    [Fact]
    public async Task Create_requires_authentication()
    {
        var client = _factory.CreateClientAs(sub: null);
        var response = await client.PostAsJsonAsync("/api/v1/competitions",
            new CreateCompetitionRequest("X", "y", "Company", null, null, 5, "accuracy"));
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_is_forbidden_without_a_creator_grant()
    {
        var client = _factory.CreateClientAs("no-grant", competitionCreator: false, "Employee");
        var response = await client.PostAsJsonAsync("/api/v1/competitions",
            new CreateCompetitionRequest("Nope", "y", "Team", null, null, 5, "accuracy"));
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Create_above_granted_scope_is_forbidden_but_within_cap_succeeds()
    {
        var client = _factory.CreateClientAs("cap-team", competitionCreator: false, "Employee");
        _factory.GrantCompetitionCreator("cap-team", VisibilityScope.Team);

        // Directorate is wider than the Team cap → forbidden.
        (await client.PostAsJsonAsync("/api/v1/competitions",
            new CreateCompetitionRequest("Too wide", "y", "Directorate", null, null, 5, "accuracy")))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Team is at the cap → allowed.
        (await client.PostAsJsonAsync("/api/v1/competitions",
            new CreateCompetitionRequest("My team", "y", "Team", null, null, 5, "accuracy")))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Platform_admin_can_create_at_any_level_without_a_grant()
    {
        var admin = _factory.CreateClientAs("plat-admin", competitionCreator: false, "Employee", "PlatformAdmin");
        (await admin.PostAsJsonAsync("/api/v1/competitions",
            new CreateCompetitionRequest("Company-wide", "y", "Company", null, null, 5, "accuracy")))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Scoring_and_leaderboard_rank_by_accuracy()
    {
        var creator = _factory.CreateClientAs("comp-creator", "Employee");
        var competitionId = await CreateCompetition(creator, revealUtc: null, quota: 5);

        // Hidden answer key: 1→A, 2→B, 3→A
        (await creator.PostAsync($"/api/v1/competitions/{competitionId}/answer-key", CsvFile("id,label\n1,A\n2,B\n3,A")))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Competitor A: perfect (3/3).
        var a = _factory.CreateClientAs("comp-a", "Employee");
        var resultA = (await (await a.PostAsync($"/api/v1/competitions/{competitionId}/submissions", CsvFile("id,label\n1,A\n2,B\n3,A")))
            .Content.ReadFromJsonAsync<SubmissionResultDto>())!;
        resultA.Score.Should().Be(1.0);

        // Competitor B: 2/3.
        var b = _factory.CreateClientAs("comp-b", "Employee");
        var resultB = (await (await b.PostAsync($"/api/v1/competitions/{competitionId}/submissions", CsvFile("id,label\n1,A\n2,A\n3,A")))
            .Content.ReadFromJsonAsync<SubmissionResultDto>())!;
        resultB.Score.Should().BeApproximately(2d / 3d, 1e-9);

        var leaderboard = (await a.GetFromJsonAsync<List<LeaderboardEntryDto>>($"/api/v1/competitions/{competitionId}/leaderboard?board=live"))!;
        leaderboard.Should().HaveCount(2);
        leaderboard[0].Rank.Should().Be(1);
        leaderboard[0].UserId.Should().Be("comp-a");
        leaderboard[0].Score.Should().Be(1.0);
        leaderboard[1].Rank.Should().Be(2);
        leaderboard[1].UserId.Should().Be("comp-b");

        // Arena enrichment: the DTO carries live stats + metric facts computed server-side.
        var dto = (await a.GetFromJsonAsync<CompetitionDto>($"/api/v1/competitions/{competitionId}"))!;
        dto.ParticipantCount.Should().Be(2);
        dto.SubmissionCount.Should().Be(2);
        dto.MetricName.Should().Be("Accuracy");
        dto.HigherIsBetter.Should().BeTrue();
        dto.QuotaPerDay.Should().Be(5);
        dto.HostName.Should().NotBeNullOrEmpty();   // falls back to the creator's user id without a profile
        dto.CreatedUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task Final_leaderboard_is_hidden_until_reveal()
    {
        var creator = _factory.CreateClientAs("comp-reveal", "Employee");
        var competitionId = await CreateCompetition(creator, revealUtc: DateTime.UtcNow.AddDays(1), quota: 5);

        var live = await creator.GetAsync($"/api/v1/competitions/{competitionId}/leaderboard?board=live");
        live.StatusCode.Should().Be(HttpStatusCode.OK);

        var final = await creator.GetAsync($"/api/v1/competitions/{competitionId}/leaderboard?board=final");
        final.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Daily_quota_is_enforced()
    {
        var creator = _factory.CreateClientAs("comp-quota", "Employee");
        var competitionId = await CreateCompetition(creator, revealUtc: null, quota: 1);
        await creator.PostAsync($"/api/v1/competitions/{competitionId}/answer-key", CsvFile("id,label\n1,A"));

        var competitor = _factory.CreateClientAs("comp-q1", "Employee");
        (await competitor.PostAsync($"/api/v1/competitions/{competitionId}/submissions", CsvFile("id,label\n1,A")))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        (await competitor.PostAsync($"/api/v1/competitions/{competitionId}/submissions", CsvFile("id,label\n1,A")))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Pipeline_submission_trains_scores_and_ranks()
    {
        var creator = _factory.CreateClientAs("pipe-creator", "Employee");
        var competitionId = await CreateCompetition(creator, revealUtc: null, quota: 25);

        // Participant-visible data: id + two features (+ label on the training set only).
        var training = new StringBuilder("id,pressure,vibration,label\n");
        for (var i = 0; i < 60; i++)
        {
            training.Append($"tr{i}p,{8 + (i % 3)},{8 + ((i / 3) % 3)},true\n");
            training.Append($"tr{i}n,{i % 3},{(i / 3) % 3},false\n");
        }
        const string evaluation = "id,pressure,vibration\ne0,9,9\ne1,0,0\ne2,8,8\ne3,1,0\n";
        const string answerKey = "id,label\ne0,true\ne1,false\ne2,true\ne3,false\n";

        (await creator.PostAsync(
            $"/api/v1/competitions/{competitionId}/datasets?labelColumn=label&idColumn=id&task=BinaryClassification",
            TwoFiles(training.ToString(), evaluation)))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await creator.PostAsync($"/api/v1/competitions/{competitionId}/answer-key", CsvFile(answerKey)))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // A participant submits only their pipeline; the server trains + predicts on the official data.
        var participant = _factory.CreateClientAs("pipe-a", "Employee");
        var definition = new WorkflowDefinition
        {
            Name = "submission",
            Nodes =
            [
                new() { Id = "d", Kind = "dataset" },
                new() { Id = "nz", Kind = "normalize" },
                new() { Id = "sp", Kind = "split" },
                new() { Id = "tr", Kind = "train" },
                new() { Id = "ev", Kind = "evaluate" },
            ],
            Edges = [new("d", "nz"), new("nz", "sp"), new("sp", "tr"), new("tr", "ev")],
        };

        var response = await participant.PostAsJsonAsync($"/api/v1/competitions/{competitionId}/submit-pipeline", definition);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var result = (await response.Content.ReadFromJsonAsync<SubmissionResultDto>())!;
        result.Score.Should().BeGreaterThan(0.8);

        var leaderboard = (await participant.GetFromJsonAsync<List<LeaderboardEntryDto>>(
            $"/api/v1/competitions/{competitionId}/leaderboard?board=live"))!;
        leaderboard.Should().ContainSingle(e => e.UserId == "pipe-a");
    }

    [Fact]
    public async Task Regression_pipeline_submission_scores_by_rmse()
    {
        var creator = _factory.CreateClientAs("reg-creator", "Employee");
        var response = await creator.PostAsJsonAsync("/api/v1/competitions",
            new CreateCompetitionRequest("Oil rate forecast", "Predict daily oil rate", "Company", null, null, 25, "rmse"));
        response.EnsureSuccessStatusCode();
        var competitionId = (await response.Content.ReadFromJsonAsync<CompetitionDto>())!.Id;

        // Deterministic linear target: oil_rate = 40*choke + 6*tubing.
        var training = new StringBuilder("id,choke,tubing_pressure,oil_rate\n");
        for (var i = 0; i < 120; i++)
        {
            var choke = 1 + (i % 8);
            var tubing = 20 + (i % 25);
            training.Append($"tr{i},{choke},{tubing},{(40 * choke) + (6 * tubing)}\n");
        }
        const string evaluation = "id,choke,tubing_pressure\ne0,3,30\ne1,5,25\ne2,2,40\n";
        const string answerKey = "id,oil_rate\ne0,300\ne1,350\ne2,320\n"; // 40*3+6*30, 40*5+6*25, 40*2+6*40

        (await creator.PostAsync(
            $"/api/v1/competitions/{competitionId}/datasets?labelColumn=oil_rate&idColumn=id&task=Regression",
            TwoFiles(training.ToString(), evaluation)))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await creator.PostAsync($"/api/v1/competitions/{competitionId}/answer-key", CsvFile(answerKey)))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var participant = _factory.CreateClientAs("reg-a", "Employee");
        var definition = new WorkflowDefinition
        {
            Name = "regression",
            Nodes =
            [
                new() { Id = "d", Kind = "dataset" },
                new() { Id = "nz", Kind = "normalize" },
                new() { Id = "sp", Kind = "split" },
                new() { Id = "tr", Kind = "train" },
                new() { Id = "ev", Kind = "evaluate" },
            ],
            Edges = [new("d", "nz"), new("nz", "sp"), new("sp", "tr"), new("tr", "ev")],
        };

        var submit = await participant.PostAsJsonAsync($"/api/v1/competitions/{competitionId}/submit-pipeline", definition);
        submit.StatusCode.Should().Be(HttpStatusCode.OK, await submit.Content.ReadAsStringAsync());
        var result = (await submit.Content.ReadFromJsonAsync<SubmissionResultDto>())!;
        result.Score.Should().NotBeNull();
        result.Score!.Value.Should().BeGreaterThanOrEqualTo(0).And.BeLessThan(150); // RMSE — real learning, not a wild miss

        var leaderboard = (await participant.GetFromJsonAsync<List<LeaderboardEntryDto>>(
            $"/api/v1/competitions/{competitionId}/leaderboard?board=live"))!;
        leaderboard.Should().ContainSingle(e => e.UserId == "reg-a");

        // RMSE competitions advertise a lower-is-better metric on the enriched DTO.
        var dto = (await participant.GetFromJsonAsync<CompetitionDto>($"/api/v1/competitions/{competitionId}"))!;
        dto.MetricName.Should().Be("RMSE");
        dto.HigherIsBetter.Should().BeFalse();
        dto.QuotaPerDay.Should().Be(25);
    }

    [Fact]
    public async Task Multiclass_pipeline_submission_scores_by_accuracy()
    {
        var creator = _factory.CreateClientAs("mc-creator", "Employee");
        var competitionId = await CreateCompetition(creator, revealUtc: null, quota: 25); // accuracy scorer

        // Three separable clusters → classes a/b/c.
        var training = new StringBuilder("id,x1,x2,grade\n");
        for (var i = 0; i < 40; i++)
        {
            foreach (var (baseVal, grade) in new[] { (0, "a"), (5, "b"), (10, "c") })
            {
                training.Append($"r{i}{grade},{baseVal + (i % 2)},{baseVal + ((i / 2) % 2)},{grade}\n");
            }
        }
        const string evaluation = "id,x1,x2\ne0,0,0\ne1,5,5\ne2,10,10\n";
        const string answerKey = "id,grade\ne0,a\ne1,b\ne2,c\n";

        (await creator.PostAsync(
            $"/api/v1/competitions/{competitionId}/datasets?labelColumn=grade&idColumn=id&task=MulticlassClassification",
            TwoFiles(training.ToString(), evaluation)))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await creator.PostAsync($"/api/v1/competitions/{competitionId}/answer-key", CsvFile(answerKey)))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var participant = _factory.CreateClientAs("mc-a", "Employee");
        var definition = new WorkflowDefinition
        {
            Name = "multiclass",
            Nodes =
            [
                new() { Id = "d", Kind = "dataset" },
                new() { Id = "nz", Kind = "normalize" },
                new() { Id = "sp", Kind = "split" },
                new() { Id = "tr", Kind = "train" },
                new() { Id = "ev", Kind = "evaluate" },
            ],
            Edges = [new("d", "nz"), new("nz", "sp"), new("sp", "tr"), new("tr", "ev")],
        };

        var submit = await participant.PostAsJsonAsync($"/api/v1/competitions/{competitionId}/submit-pipeline", definition);
        submit.StatusCode.Should().Be(HttpStatusCode.OK, await submit.Content.ReadAsStringAsync());
        var result = (await submit.Content.ReadFromJsonAsync<SubmissionResultDto>())!;
        result.Score.Should().Be(1.0); // all three held-out classes predicted correctly

        var leaderboard = (await participant.GetFromJsonAsync<List<LeaderboardEntryDto>>(
            $"/api/v1/competitions/{competitionId}/leaderboard?board=live"))!;
        leaderboard.Should().ContainSingle(e => e.UserId == "mc-a");
    }

    [Fact]
    public async Task Concluding_a_competition_closes_submissions()
    {
        var creator = _factory.CreateClientAs("life-creator", "Employee");
        var competitionId = await CreateCompetition(creator, revealUtc: null, quota: 25);
        await creator.PostAsync($"/api/v1/competitions/{competitionId}/answer-key", CsvFile("id,label\n1,A"));

        var competitor = _factory.CreateClientAs("life-a", "Employee");
        (await competitor.PostAsync($"/api/v1/competitions/{competitionId}/submissions", CsvFile("id,label\n1,A")))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        // Conclude → further submissions are rejected.
        (await creator.PostAsJsonAsync($"/api/v1/competitions/{competitionId}/status", new SetStatusRequest("concluded")))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await competitor.PostAsync($"/api/v1/competitions/{competitionId}/submissions", CsvFile("id,label\n1,A")))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Only_the_creator_can_change_lifecycle()
    {
        var creator = _factory.CreateClientAs("life-owner", "Employee");
        var competitionId = await CreateCompetition(creator, revealUtc: null, quota: 5);

        var other = _factory.CreateClientAs("life-other", "Employee");
        (await other.PostAsJsonAsync($"/api/v1/competitions/{competitionId}/status", new SetStatusRequest("concluded")))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await creator.PostAsJsonAsync($"/api/v1/competitions/{competitionId}/status", new SetStatusRequest("bogus")))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private static async Task<Guid> CreateCompetition(HttpClient client, DateTime? revealUtc, int quota)
    {
        var response = await client.PostAsJsonAsync("/api/v1/competitions",
            new CreateCompetitionRequest("Spot the pump", "Flag ESP failures", "Company", null, revealUtc, quota, "accuracy"));
        response.EnsureSuccessStatusCode();
        var dto = (await response.Content.ReadFromJsonAsync<CompetitionDto>())!;
        return dto.Id;
    }

    private static MultipartFormDataContent CsvFile(string content)
    {
        var form = new MultipartFormDataContent();
        var part = new StringContent(content);
        part.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        form.Add(part, "file", "data.csv");
        return form;
    }

    private static MultipartFormDataContent TwoFiles(string training, string evaluation)
    {
        var form = new MultipartFormDataContent();
        var t = new StringContent(training);
        t.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        form.Add(t, "training", "training.csv");
        var e = new StringContent(evaluation);
        e.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        form.Add(e, "evaluation", "evaluation.csv");
        return form;
    }
}
