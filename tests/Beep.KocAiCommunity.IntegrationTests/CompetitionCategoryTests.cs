using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Beep.KocAiCommunity.Contracts.Competitions;
using Beep.KocAiCommunity.Domain.Competitions;
using Beep.KocAiCommunity.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Beep.KocAiCommunity.IntegrationTests;

/// <summary>
/// Turning a category off must hide its competitions from <b>every</b> route, not just the browse list.
/// Six routes reach a competition, and a half-applied rule would leave a direct link — and the host's
/// training data behind it — open to anyone who kept the URL.
/// </summary>
public class CompetitionCategoryTests(KocApiFactory factory) : IClassFixture<KocApiFactory>
{
    private readonly KocApiFactory _factory = factory;

    /// <summary>Creates a category and puts a competition in it, returning the competition's id.</summary>
    private async Task<Guid> CompetitionInCategory(HttpClient host, string categoryCode, bool enabled = true)
    {
        var response = await host.PostAsJsonAsync("/api/v1/competitions",
            new CreateCompetitionRequest($"Challenge {categoryCode}", "A challenge.", "Company", null, null, 5, "accuracy"));
        response.EnsureSuccessStatusCode();
        var competitionId = (await response.Content.ReadFromJsonAsync<CompetitionDto>())!.Id;

        (await host.PostAsync($"/api/v1/competitions/{competitionId}/answer-key", CsvFile("id,label\n1,1\n")))
            .EnsureSuccessStatusCode();
        (await host.PostAsJsonAsync($"/api/v1/competitions/{competitionId}/status", new SetStatusRequest("active")))
            .EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KocDbContext>();

        if (!await db.CompetitionCategories.AnyAsync(c => c.Code == categoryCode))
        {
            db.CompetitionCategories.Add(new CompetitionCategory
            {
                Code = categoryCode,
                Name = categoryCode,
                IsEnabled = enabled,
                CreatedUtc = DateTime.UtcNow,
            });
        }

        var competition = await db.Competitions.FirstAsync(c => c.Id == competitionId);
        competition.CategoryCode = categoryCode;
        await db.SaveChangesAsync();

        return competitionId;
    }

    private async Task SetCategoryEnabled(string code, bool enabled)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KocDbContext>();
        var category = await db.CompetitionCategories.FirstAsync(c => c.Code == code);
        category.IsEnabled = enabled;
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Disabling_a_category_closes_every_route_to_its_competitions()
    {
        var host = _factory.CreateClientAs("cat-host", "Employee");
        var id = await CompetitionInCategory(host, "cat-hide-all");

        // Visible while the category is on.
        (await host.GetAsync($"/api/v1/competitions/{id}")).StatusCode.Should().Be(HttpStatusCode.OK);

        await SetCategoryEnabled("cat-hide-all", false);

        // The list no longer carries it...
        var listed = await host.GetFromJsonAsync<List<CompetitionDto>>("/api/v1/competitions");
        listed!.Should().NotContain(c => c.Id == id);

        // ...and neither does the anonymous showcase.
        var showcase = await _factory.CreateClientAs(sub: null)
            .GetFromJsonAsync<PublicShowcaseDto>("/api/v1/public/showcase");
        showcase!.Competitions.Should().NotContain(c => c.Id == id);

        // Every direct route answers as though it isn't there. This is the half that a UI-only filter
        // would miss — the data download in particular is the host's training set.
        (await host.GetAsync($"/api/v1/competitions/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await host.GetAsync($"/api/v1/competitions/{id}/leaderboard")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await host.GetAsync($"/api/v1/competitions/{id}/data/training")).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var mine = await host.GetFromJsonAsync<List<SubmissionResultDto>>($"/api/v1/competitions/{id}/submissions");
        mine!.Should().BeEmpty();
    }

    [Fact]
    public async Task Re_enabling_restores_the_competition_untouched()
    {
        var host = _factory.CreateClientAs("cat-restore", "Employee");
        var id = await CompetitionInCategory(host, "cat-restore-me");

        await SetCategoryEnabled("cat-restore-me", false);
        (await host.GetAsync($"/api/v1/competitions/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        await SetCategoryEnabled("cat-restore-me", true);

        // Nothing was deleted — hiding is reversible, which is what makes it safe for staging a season.
        var restored = await host.GetFromJsonAsync<CompetitionDto>($"/api/v1/competitions/{id}");
        restored!.Id.Should().Be(id);
        (await host.GetAsync($"/api/v1/competitions/{id}/leaderboard")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task An_uncategorised_competition_is_never_hidden()
    {
        var host = _factory.CreateClientAs("cat-none", "Employee");

        // Competitions created before categories existed carry no code, and must keep working.
        var response = await host.PostAsJsonAsync("/api/v1/competitions",
            new CreateCompetitionRequest("Uncategorised", "No category.", "Company", null, null, 5, "accuracy"));
        var id = (await response.Content.ReadFromJsonAsync<CompetitionDto>())!.Id;
        (await host.PostAsync($"/api/v1/competitions/{id}/answer-key", CsvFile("id,label\n1,1\n")))
            .EnsureSuccessStatusCode();
        (await host.PostAsJsonAsync($"/api/v1/competitions/{id}/status", new SetStatusRequest("active")))
            .EnsureSuccessStatusCode();

        await CompetitionInCategory(host, "cat-unrelated");
        await SetCategoryEnabled("cat-unrelated", false);

        (await host.GetAsync($"/api/v1/competitions/{id}")).StatusCode.Should().Be(HttpStatusCode.OK);
        var listed = await host.GetFromJsonAsync<List<CompetitionDto>>("/api/v1/competitions");
        listed!.Should().Contain(c => c.Id == id);
    }

    [Fact]
    public async Task Submitting_to_a_hidden_competition_is_refused()
    {
        var host = _factory.CreateClientAs("cat-submit-host", "Employee");
        var id = await CompetitionInCategory(host, "cat-no-submit");

        (await host.PostAsync($"/api/v1/competitions/{id}/answer-key", CsvFile("id,label\n1,1\n")))
            .EnsureSuccessStatusCode();

        await SetCategoryEnabled("cat-no-submit", false);

        var entrant = _factory.CreateClientAs("cat-entrant", "Employee");
        (await entrant.PostAsync($"/api/v1/competitions/{id}/submissions", CsvFile("id,label\n1,1\n")))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
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
