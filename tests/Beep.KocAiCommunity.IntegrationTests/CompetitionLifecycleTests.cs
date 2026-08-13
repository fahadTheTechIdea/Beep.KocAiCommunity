using System.Net;
using System.Net.Http.Json;
using Beep.KocAiCommunity.Application.Competitions;
using Beep.KocAiCommunity.Contracts.Competitions;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Beep.KocAiCommunity.IntegrationTests;

/// <summary>
/// Phase 24's contract: task and metric are one bound decision, "draft" is a true word, and the Host
/// console's powers belong to whoever manages the competition — its creator or a platform admin.
/// </summary>
public sealed class CompetitionLifecycleTests(KocApiFactory factory) : IClassFixture<KocApiFactory>
{
    private static MultipartFormDataContent CsvFile(string content, string field = "file")
    {
        var form = new MultipartFormDataContent();
        var part = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(content));
        part.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv");
        form.Add(part, field, "data.csv");
        return form;
    }

    private static async Task<CompetitionDto> CreateAsync(HttpClient client, string title,
        string scorer = "accuracy", string? task = null)
    {
        var response = await client.PostAsJsonAsync("/api/v1/competitions",
            new CreateCompetitionRequest(title, "predict something real", "Company", null, null, 5, scorer, TaskType: task));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CompetitionDto>())!;
    }

    [Fact]
    public async Task The_task_travels_with_the_scorer_and_a_mismatched_pair_is_refused()
    {
        var host = factory.CreateClientAs("pair-host", "Employee");

        // The pair, stored together: an rmse competition created as Forecasting IS Forecasting —
        // before this phase the dialog's task choice was discarded and every new competition claimed
        // to be binary classification whatever its metric said.
        var created = await CreateAsync(host, "Pairing works", scorer: "rmse", task: "Forecasting");
        created.TaskType.Should().Be("Forecasting");
        created.MetricName.Should().Be("RMSE");

        // Left unstated, the task falls back into the scorer's family instead of a hardcoded default.
        var defaulted = await CreateAsync(host, "Pairing defaults", scorer: "auc");
        defaulted.TaskType.Should().Be("AnomalyDetection");

        // And a pair that cannot score is refused at birth, not discovered at upload time.
        var mismatch = await host.PostAsJsonAsync("/api/v1/competitions",
            new CreateCompetitionRequest("Nope", "d", "Company", null, null, 5, "rmse", TaskType: "BinaryClassification"));
        mismatch.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await mismatch.Content.ReadAsStringAsync()).Should().Contain("cannot score");
    }

    [Fact]
    public async Task A_draft_is_visible_to_its_host_and_admins_and_to_nobody_else()
    {
        var host = factory.CreateClientAs("draft-host", "Employee");
        var created = await CreateAsync(host, "Quiet draft");
        created.Status.Should().Be("draft", "creation makes drafts — which is what the launcher promises");

        // The host keeps the route back to their own work…
        var mine = await host.GetFromJsonAsync<List<CompetitionDto>>("/api/v1/competitions");
        mine!.Should().Contain(c => c.Id == created.Id);

        // …an admin can see it to help…
        var admin = factory.CreateClientAs("draft-admin", "Employee", "PlatformAdmin");
        (await admin.GetFromJsonAsync<List<CompetitionDto>>("/api/v1/competitions"))!
            .Should().Contain(c => c.Id == created.Id);

        // …and to its audience it does not exist yet.
        var colleague = factory.CreateClientAs("draft-passerby", "Employee");
        (await colleague.GetFromJsonAsync<List<CompetitionDto>>("/api/v1/competitions"))!
            .Should().NotContain(c => c.Id == created.Id);
    }

    [Fact]
    public async Task Activation_refuses_until_the_answer_key_exists()
    {
        var host = factory.CreateClientAs("gate-host", "Employee");
        var created = await CreateAsync(host, "Needs a key");

        var early = await host.PostAsJsonAsync($"/api/v1/competitions/{created.Id}/status", new SetStatusRequest("active"));
        early.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await early.Content.ReadAsStringAsync()).Should().Contain("answer key");

        (await host.PostAsync($"/api/v1/competitions/{created.Id}/answer-key", CsvFile("id,label\n1,yes")))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await host.PostAsJsonAsync($"/api/v1/competitions/{created.Id}/status", new SetStatusRequest("active")))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task The_console_edits_words_rules_domain_and_prizes_for_its_manager_only()
    {
        var host = factory.CreateClientAs("console-host", "Employee");
        var created = await CreateAsync(host, "Console editable");

        // A colleague WITH a hosting grant is still not this competition's manager.
        var stranger = factory.CreateClientAs("console-stranger", "Employee");
        (await stranger.PutAsJsonAsync($"/api/v1/competitions/{created.Id}",
                new UpdateCompetitionRequest("Hijacked", "mine now", 9)))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // The host edits the English — the words were frozen at creation for far too long.
        (await host.PutAsJsonAsync($"/api/v1/competitions/{created.Id}",
                new UpdateCompetitionRequest("Console edited", "a better brief", 9, "Company")))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Domain and prizes are the host's own calls now, not admin errands.
        (await host.PutAsJsonAsync($"/api/v1/competitions/{created.Id}/category", new SetCompetitionCategoryRequest("production")))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await host.PostAsJsonAsync($"/api/v1/competitions/{created.Id}/prizes",
                new SetPrizesRequest("Gold day off", null, null)))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var read = (await host.GetFromJsonAsync<CompetitionDto>($"/api/v1/competitions/{created.Id}"))!;
        read.Title.Should().Be("Console edited");
        read.QuotaPerDay.Should().Be(9);
        read.CategoryCode.Should().Be("production");
        read.FirstPrize.Should().Be("Gold day off");
        read.CanManage.Should().BeTrue();

        // The same read tells a non-manager the console is not theirs.
        (await stranger.GetFromJsonAsync<CompetitionDto>($"/api/v1/competitions/{created.Id}"))!
            .CanManage.Should().BeFalse("the draft is invisible in browse, and even once active the console belongs to the host");
    }

    [Fact]
    public async Task The_audience_is_free_while_draft_and_frozen_once_active()
    {
        var host = factory.CreateClientAs("scope-host", "Employee");
        var created = await CreateAsync(host, "Scoped");

        (await host.PutAsJsonAsync($"/api/v1/competitions/{created.Id}",
                new UpdateCompetitionRequest("Scoped", "d", 5, "Team")))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        await host.PostAsync($"/api/v1/competitions/{created.Id}/answer-key", CsvFile("id,label\n1,yes"));
        await host.PostAsJsonAsync($"/api/v1/competitions/{created.Id}/status", new SetStatusRequest("active"));

        var widen = await host.PutAsJsonAsync($"/api/v1/competitions/{created.Id}",
            new UpdateCompetitionRequest("Scoped", "d", 5, "Company"));
        widen.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await widen.Content.ReadAsStringAsync()).Should().Contain("draft");
    }

    [Fact]
    public async Task An_admin_manages_a_competition_they_did_not_create()
    {
        // The seeded-competition rescue: every seeded challenge belongs to "koc-platform", a user who
        // cannot sign in — before the uniform manage gate, nobody could conclude or reprice them.
        var host = factory.CreateClientAs("rescue-host", "Employee");
        var created = await CreateAsync(host, "Rescued");
        await host.PostAsync($"/api/v1/competitions/{created.Id}/answer-key", CsvFile("id,label\n1,yes"));

        var admin = factory.CreateClientAs("rescue-admin", "Employee", "PlatformAdmin");
        (await admin.PostAsJsonAsync($"/api/v1/competitions/{created.Id}/status", new SetStatusRequest("active")))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await admin.PostAsJsonAsync($"/api/v1/competitions/{created.Id}/prizes", new SetPrizesRequest(null, "Silver", null)))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await admin.GetFromJsonAsync<CompetitionDto>($"/api/v1/competitions/{created.Id}"))!
            .CanManage.Should().BeTrue("a platform admin manages every competition");
    }

    [Fact]
    public async Task The_remaining_quota_rides_the_competition_read()
    {
        var host = factory.CreateClientAs("quota-host", "Employee");
        var created = await CreateAsync(host, "Quota visible");
        await host.PostAsync($"/api/v1/competitions/{created.Id}/answer-key", CsvFile("id,label\n1,yes\n2,no"));
        await host.PostAsJsonAsync($"/api/v1/competitions/{created.Id}/status", new SetStatusRequest("active"));

        var player = factory.CreateClientAs("quota-player", "Employee");
        (await player.GetFromJsonAsync<CompetitionDto>($"/api/v1/competitions/{created.Id}"))!
            .MyQuotaRemainingToday.Should().Be(5);

        (await player.PostAsync($"/api/v1/competitions/{created.Id}/submissions", CsvFile("id,label\n1,yes\n2,no")))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        (await player.GetFromJsonAsync<CompetitionDto>($"/api/v1/competitions/{created.Id}"))!
            .MyQuotaRemainingToday.Should().Be(4, "the strip must always describe what a submission just spent");
    }

    [Fact]
    public async Task A_competition_concludes_itself_at_reveal_when_asked_to()
    {
        var host = factory.CreateClientAs("reveal-host", "Employee");
        var created = await CreateAsync(host, "Self closing");
        await host.PostAsync($"/api/v1/competitions/{created.Id}/answer-key", CsvFile("id,label\n1,yes"));
        await host.PostAsJsonAsync($"/api/v1/competitions/{created.Id}/status", new SetStatusRequest("active"));

        // A reveal already in the past, with the conclude choice on — the scheduler's next tick owns it.
        (await host.PostAsJsonAsync($"/api/v1/competitions/{created.Id}/reveal",
                new SetRevealRequest(DateTime.UtcNow.AddMinutes(-1), ConcludeAtReveal: true)))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // The tick, invoked directly: the hosted timer is background machinery and stays off in tests.
        using var scope = factory.Services.CreateScope();
        var competitions = scope.ServiceProvider.GetRequiredService<ICompetitionService>();
        (await competitions.ConcludeDueAsync()).Should().BeGreaterThanOrEqualTo(1);

        (await host.GetFromJsonAsync<CompetitionDto>($"/api/v1/competitions/{created.Id}"))!
            .Status.Should().Be("concluded");
    }
}
