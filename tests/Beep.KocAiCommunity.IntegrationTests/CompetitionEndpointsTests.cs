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

        // Hidden answer key — every id labelled A, so any public/private holdout subset scores identically
        // (the scoring split is deterministic per id, so a mixed key would make the public score depend on
        // which ids landed in the public half; an all-A key keeps this test about ranking, not the split).
        (await creator.PostAsync($"/api/v1/competitions/{competitionId}/answer-key", CsvFile("id,label\n1,A\n2,A\n3,A\n4,A\n5,A\n6,A")))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await creator.PostAsJsonAsync($"/api/v1/competitions/{competitionId}/status", new SetStatusRequest("active")))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Competitor A: perfect on every id → 1.0 on any subset.
        var a = _factory.CreateClientAs("comp-a", "Employee");
        var resultA = (await (await a.PostAsync($"/api/v1/competitions/{competitionId}/submissions", CsvFile("id,label\n1,A\n2,A\n3,A\n4,A\n5,A\n6,A")))
            .Content.ReadFromJsonAsync<SubmissionResultDto>())!;
        resultA.Score.Should().Be(1.0);

        // Competitor B: wrong on every id → 0.0 on any subset.
        var b = _factory.CreateClientAs("comp-b", "Employee");
        var resultB = (await (await b.PostAsync($"/api/v1/competitions/{competitionId}/submissions", CsvFile("id,label\n1,B\n2,B\n3,B\n4,B\n5,B\n6,B")))
            .Content.ReadFromJsonAsync<SubmissionResultDto>())!;
        resultB.Score.Should().Be(0.0);

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
        (await creator.PostAsJsonAsync($"/api/v1/competitions/{competitionId}/status", new SetStatusRequest("active")))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var competitor = _factory.CreateClientAs("comp-q1", "Employee");
        (await competitor.PostAsync($"/api/v1/competitions/{competitionId}/submissions", CsvFile("id,label\n1,A")))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        (await competitor.PostAsync($"/api/v1/competitions/{competitionId}/submissions", CsvFile("id,label\n1,A")))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Submission_missing_predictions_for_some_ids_is_rejected()
    {
        var creator = _factory.CreateClientAs("comp-cover", "Employee");
        var competitionId = await CreateCompetition(creator, revealUtc: null, quota: 5);
        (await creator.PostAsync($"/api/v1/competitions/{competitionId}/answer-key", CsvFile("id,label\n1,A\n2,A\n3,A")))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await creator.PostAsJsonAsync($"/api/v1/competitions/{competitionId}/status", new SetStatusRequest("active")))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Predictions cover only 1 of the 3 answer-key ids → must be rejected, not silently scored on a subset.
        var competitor = _factory.CreateClientAs("comp-cover-a", "Employee");
        var response = await competitor.PostAsync($"/api/v1/competitions/{competitionId}/submissions", CsvFile("id,label\n1,A"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("missing predictions");
    }

    [Fact]
    public async Task Admin_can_set_podium_prizes()
    {
        var admin = _factory.CreateClientAs("prize-admin", "Employee", "PlatformAdmin");
        var competitionId = await CreateCompetition(admin, revealUtc: null, quota: 5);

        (await admin.PostAsJsonAsync($"/api/v1/competitions/{competitionId}/prizes",
            new SetPrizesRequest("1,000 Barrels + Gusher trophy", "500 Barrels", "250 Barrels")))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var dto = (await admin.GetFromJsonAsync<CompetitionDto>($"/api/v1/competitions/{competitionId}"))!;
        dto.FirstPrize.Should().Be("1,000 Barrels + Gusher trophy");
        dto.SecondPrize.Should().Be("500 Barrels");
        dto.ThirdPrize.Should().Be("250 Barrels");

        // Blank clears a prize.
        (await admin.PostAsJsonAsync($"/api/v1/competitions/{competitionId}/prizes",
            new SetPrizesRequest("1,000 Barrels", "", null)))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        var cleared = (await admin.GetFromJsonAsync<CompetitionDto>($"/api/v1/competitions/{competitionId}"))!;
        cleared.SecondPrize.Should().BeNull();
        cleared.ThirdPrize.Should().BeNull();
    }

    [Fact]
    public async Task Host_can_set_the_hero_image_path()
    {
        var host = _factory.CreateClientAs("hero-host", "Employee");
        var competitionId = await CreateCompetition(host, revealUtc: null, quota: 5);

        var path = $"/uploads/competitions/{competitionId}.png";
        (await host.PostAsJsonAsync($"/api/v1/competitions/{competitionId}/hero-image", new SetHeroImagePathRequest(path)))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var dto = (await host.GetFromJsonAsync<CompetitionDto>($"/api/v1/competitions/{competitionId}"))!;
        dto.HasHeroImage.Should().BeTrue();
        dto.HeroImagePath.Should().Be(path);
    }

    [Fact]
    public async Task Platform_admin_can_set_hero_image_for_a_competition_they_did_not_create()
    {
        var creator = _factory.CreateClientAs("hero-creator", "Employee");
        var competitionId = await CreateCompetition(creator, revealUtc: null, quota: 5);

        var admin = _factory.CreateClientAs("hero-admin", "Employee", "PlatformAdmin");
        var path = $"/uploads/competitions/{competitionId}.jpg";
        (await admin.PostAsJsonAsync($"/api/v1/competitions/{competitionId}/hero-image", new SetHeroImagePathRequest(path)))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var dto = (await admin.GetFromJsonAsync<CompetitionDto>($"/api/v1/competitions/{competitionId}"))!;
        dto.HeroImagePath.Should().Be(path);
    }

    [Fact]
    public async Task Public_showcase_is_reachable_without_signing_in()
    {
        // A signed-in host runs an active, company-wide competition.
        var host = _factory.CreateClientAs("showcase-host", "Employee");
        var competitionId = await CreateCompetition(host, revealUtc: null, quota: 5);
        (await host.PostAsync($"/api/v1/competitions/{competitionId}/answer-key", CsvFile("id,label\n1,1\n")))
            .EnsureSuccessStatusCode();
        (await host.PostAsJsonAsync($"/api/v1/competitions/{competitionId}/status", new SetStatusRequest("active")))
            .EnsureSuccessStatusCode();

        // A guest (no auth) can read the landing showcase and see that competition.
        var guest = _factory.CreateClientAs(sub: null);
        var response = await guest.GetAsync("/api/v1/public/showcase");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var showcase = (await response.Content.ReadFromJsonAsync<PublicShowcaseDto>())!;
        showcase.Competitions.Should().Contain(c => c.Id == competitionId);
    }

    [Fact]
    public async Task The_public_showcase_shows_the_same_names_a_member_sees()
    {
        // The showcase used to mask names to initials. It no longer does: the platform shows a visitor
        // the same company data any employee sees, and a leaderboard whose names are hidden is not much
        // of a leaderboard. Signing in is for taking part, not for reading.
        var host = _factory.CreateClientAs("showcase-host", "Employee");
        var competitionId = await CreateCompetition(host, revealUtc: null, quota: 5);
        (await host.PostAsync($"/api/v1/competitions/{competitionId}/answer-key", CsvFile("id,label\n1,1\n2,0\n")))
            .EnsureSuccessStatusCode();
        (await host.PostAsJsonAsync($"/api/v1/competitions/{competitionId}/status", new SetStatusRequest("active")))
            .EnsureSuccessStatusCode();
        var competitor = _factory.CreateClientAs("Fahad Al-Dhubaib", "Employee");
        (await competitor.PostAsync($"/api/v1/competitions/{competitionId}/submissions", CsvFile("id,label\n1,1\n2,0\n")))
            .EnsureSuccessStatusCode();

        var showcase = (await _factory.CreateClientAs(sub: null)
            .GetFromJsonAsync<PublicShowcaseDto>("/api/v1/public/showcase"))!;

        // Asserted on the learners board rather than the featured one: which competition is featured
        // depends on the seed, and the previous version of this test looped over a board that was empty
        // — passing without checking anything at all.
        showcase.TopLearners.Should().NotBeEmpty("the submission above earns Barrels");
        showcase.TopLearners.Should().Contain(l => l.DisplayName.Contains("Al-Dhubaib"),
            "the visitor sees the same name a signed-in colleague sees");
        showcase.TopLearners.Should().OnlyContain(l => !string.IsNullOrEmpty(l.UserId),
            "and the id, so their profile can be opened");
    }

    [Fact]
    public async Task The_public_showcase_carries_a_full_board_not_a_podium()
    {
        // The landing page shows the standings at full size, so three rows is no longer enough.
        var showcase = (await _factory.CreateClientAs(sub: null)
            .GetFromJsonAsync<PublicShowcaseDto>("/api/v1/public/showcase"))!;

        showcase.FeaturedBoard.Count.Should().BeLessThanOrEqualTo(10);
        showcase.TopLearners.Count.Should().BeLessThanOrEqualTo(10);
    }

    [Fact]
    public async Task A_non_creator_non_admin_cannot_set_the_hero_image()
    {
        var creator = _factory.CreateClientAs("hero-owner", "Employee");
        var competitionId = await CreateCompetition(creator, revealUtc: null, quota: 5);

        var stranger = _factory.CreateClientAs("hero-stranger", "Employee");
        (await stranger.PostAsJsonAsync($"/api/v1/competitions/{competitionId}/hero-image",
            new SetHeroImagePathRequest("/uploads/competitions/x.png")))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }






    [Fact]
    public async Task Concluding_a_competition_closes_submissions()
    {
        var creator = _factory.CreateClientAs("life-creator", "Employee");
        var competitionId = await CreateCompetition(creator, revealUtc: null, quota: 25);
        await creator.PostAsync($"/api/v1/competitions/{competitionId}/answer-key", CsvFile("id,label\n1,A"));
        (await creator.PostAsJsonAsync($"/api/v1/competitions/{competitionId}/status", new SetStatusRequest("active")))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var competitor = _factory.CreateClientAs("life-a", "Employee");
        (await competitor.PostAsync($"/api/v1/competitions/{competitionId}/submissions", CsvFile("id,label\n1,A")))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        // Conclude → further submissions are rejected.
        (await creator.PostAsJsonAsync($"/api/v1/competitions/{competitionId}/status", new SetStatusRequest("concluded")))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await competitor.PostAsync($"/api/v1/competitions/{competitionId}/submissions", CsvFile("id,label\n1,A")))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Participant notifications deep-link to the competition's arena page.
        var notifications = (await competitor.GetFromJsonAsync<List<Beep.KocAiCommunity.Contracts.Notifications.NotificationDto>>("/api/v1/notifications"))!;
        notifications.Should().Contain(n => n.Type == "competition-concluded" && n.LinkUrl == $"/compete/{competitionId}");
        notifications.Should().Contain(n => n.Type == "submission-scored" && n.LinkUrl == $"/compete/{competitionId}");
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

    [Fact]
    public async Task Regression_task_with_accuracy_scorer_is_rejected()
    {
        var creator = _factory.CreateClientAs("bad-pairing", "Employee");
        var competitionId = await CreateCompetition(creator, revealUtc: null, quota: 5); // scorer = accuracy

        var response = await creator.PostAsync(
            $"/api/v1/competitions/{competitionId}/datasets?labelColumn=y&idColumn=id&task=Regression",
            TwoFiles("id,x,y\n1,1,2\n", "id,x\ne0,1\n"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Answer_key_missing_an_evaluation_id_is_rejected()
    {
        var creator = _factory.CreateClientAs("bad-key", "Employee");
        var competitionId = await CreateCompetition(creator, revealUtc: null, quota: 5);

        (await creator.PostAsync(
            $"/api/v1/competitions/{competitionId}/datasets?labelColumn=label&idColumn=id&task=BinaryClassification",
            TwoFiles("id,x,label\n1,1,true\n", "id,x\ne0,1\ne1,2\n")))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Key covers e0 but not e1 → rejected.
        var response = await creator.PostAsync($"/api/v1/competitions/{competitionId}/answer-key", CsvFile("id,label\ne0,true"));
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Replacing_the_answer_key_rescores_submissions_and_rebuilds_the_leaderboard()
    {
        var creator = _factory.CreateClientAs("rekey-creator", "Employee");
        var competitionId = await CreateCompetition(creator, revealUtc: null, quota: 5);

        // Key K1: every id is A (all-A keeps the assertion independent of the public/private split).
        (await creator.PostAsync($"/api/v1/competitions/{competitionId}/answer-key", CsvFile("id,label\n1,A\n2,A\n3,A\n4,A")))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await creator.PostAsJsonAsync($"/api/v1/competitions/{competitionId}/status", new SetStatusRequest("active")))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // X is perfect under K1 (all A); Y is perfect under the *future* K2 (all B) → wrong under K1.
        var x = _factory.CreateClientAs("rekey-x", "Employee");
        (await (await x.PostAsync($"/api/v1/competitions/{competitionId}/submissions", CsvFile("id,label\n1,A\n2,A\n3,A\n4,A")))
            .Content.ReadFromJsonAsync<SubmissionResultDto>())!.Score.Should().Be(1.0);
        var y = _factory.CreateClientAs("rekey-y", "Employee");
        await y.PostAsync($"/api/v1/competitions/{competitionId}/submissions", CsvFile("id,label\n1,B\n2,B\n3,B\n4,B"));

        // Under K1, X leads.
        var before = (await x.GetFromJsonAsync<List<LeaderboardEntryDto>>($"/api/v1/competitions/{competitionId}/leaderboard?board=live"))!;
        before[0].UserId.Should().Be("rekey-x");

        // Creator swaps in K2 (all B) — now Y is perfect and X is wrong. Existing submissions must be rescored.
        (await creator.PostAsync($"/api/v1/competitions/{competitionId}/answer-key", CsvFile("id,label\n1,B\n2,B\n3,B\n4,B")))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var after = (await x.GetFromJsonAsync<List<LeaderboardEntryDto>>($"/api/v1/competitions/{competitionId}/leaderboard?board=live"))!;
        after[0].UserId.Should().Be("rekey-y", "the board must reflect the new key, not a mix");
        after[0].Score.Should().Be(1.0);
        after.Single(e => e.UserId == "rekey-x").Score.Should().Be(0.0);
    }

    [Fact]
    public async Task A_concluded_competitions_answer_key_is_locked()
    {
        var creator = _factory.CreateClientAs("lock-creator", "Employee");
        var competitionId = await CreateCompetition(creator, revealUtc: null, quota: 5);
        (await creator.PostAsync($"/api/v1/competitions/{competitionId}/answer-key", CsvFile("id,label\n1,A")))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await creator.PostAsJsonAsync($"/api/v1/competitions/{competitionId}/status", new SetStatusRequest("concluded")))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // The results are final — the key can no longer be changed.
        (await creator.PostAsync($"/api/v1/competitions/{competitionId}/answer-key", CsvFile("id,label\n1,B")))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Final_board_reveals_the_private_holdout_after_the_reveal_time()
    {
        // Reveal time already passed → the concealed final board (ranked on the hidden private holdout) is
        // served alongside the live public board. (The future-reveal 403 gate is covered separately.)
        var creator = _factory.CreateClientAs("holdout-creator", "Employee");
        var competitionId = await CreateCompetition(creator, revealUtc: DateTime.UtcNow.AddMinutes(-5), quota: 5);
        (await creator.PostAsync($"/api/v1/competitions/{competitionId}/answer-key", CsvFile("id,label\n1,A\n2,A\n3,A\n4,A\n5,A\n6,A")))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await creator.PostAsJsonAsync($"/api/v1/competitions/{competitionId}/status", new SetStatusRequest("active")))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var top = _factory.CreateClientAs("holdout-top", "Employee");
        await top.PostAsync($"/api/v1/competitions/{competitionId}/submissions", CsvFile("id,label\n1,A\n2,A\n3,A\n4,A\n5,A\n6,A")); // perfect
        var bottom = _factory.CreateClientAs("holdout-bottom", "Employee");
        await bottom.PostAsync($"/api/v1/competitions/{competitionId}/submissions", CsvFile("id,label\n1,B\n2,B\n3,B\n4,B\n5,B\n6,B")); // wrong

        var live = (await top.GetFromJsonAsync<List<LeaderboardEntryDto>>($"/api/v1/competitions/{competitionId}/leaderboard?board=live"))!;
        live[0].UserId.Should().Be("holdout-top");
        live[0].Score.Should().Be(1.0);

        var finalResponse = await top.GetAsync($"/api/v1/competitions/{competitionId}/leaderboard?board=final");
        finalResponse.StatusCode.Should().Be(HttpStatusCode.OK); // reveal has passed
        var final = (await finalResponse.Content.ReadFromJsonAsync<List<LeaderboardEntryDto>>())!;
        final[0].UserId.Should().Be("holdout-top");
        final[0].Score.Should().Be(1.0); // perfect on the private half too
        final.Single(e => e.UserId == "holdout-bottom").Score.Should().Be(0.0);
    }

    [Fact]
    public async Task Platform_admin_pins_the_featured_competition_and_only_one_stays_featured()
    {
        var admin = _factory.CreateClientAs("feat-admin", competitionCreator: false, "Employee", "PlatformAdmin");
        var a = await CreateCompetition(admin, revealUtc: null, quota: 5);
        var b = await CreateCompetition(admin, revealUtc: null, quota: 5);

        (await admin.PostAsync($"/api/v1/competitions/{a}/feature", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await admin.GetFromJsonAsync<CompetitionDto>($"/api/v1/competitions/{a}"))!.IsFeatured.Should().BeTrue();

        // Featuring b makes it the hero and clears a — exactly one at a time.
        (await admin.PostAsync($"/api/v1/competitions/{b}/feature", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await admin.GetFromJsonAsync<CompetitionDto>($"/api/v1/competitions/{b}"))!.IsFeatured.Should().BeTrue();
        (await admin.GetFromJsonAsync<CompetitionDto>($"/api/v1/competitions/{a}"))!.IsFeatured.Should().BeFalse();
    }

    [Fact]
    public async Task A_non_admin_cannot_set_the_featured_competition()
    {
        var admin = _factory.CreateClientAs("feat-admin2", competitionCreator: false, "Employee", "PlatformAdmin");
        var id = await CreateCompetition(admin, revealUtc: null, quota: 5);

        var other = _factory.CreateClientAs("feat-noadmin", "Employee");
        (await other.PostAsync($"/api/v1/competitions/{id}/feature", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
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
