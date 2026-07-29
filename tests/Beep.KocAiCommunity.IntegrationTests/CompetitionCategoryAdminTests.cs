using System.Net;
using System.Net.Http.Json;
using Beep.KocAiCommunity.Contracts.Admin;
using Beep.KocAiCommunity.Contracts.Competitions;
using FluentAssertions;
using Xunit;

namespace Beep.KocAiCommunity.IntegrationTests;

/// <summary>
/// The admin surface for the catalogue: who owns the category list, and the learn ↔ compete links that
/// until now only the seeder could set.
/// </summary>
public class CompetitionCategoryAdminTests(KocApiFactory factory) : IClassFixture<KocApiFactory>
{
    private readonly KocApiFactory _factory = factory;

    private HttpClient Admin(string sub) => _factory.CreateClientAs(sub, "Manager", "PlatformAdmin");

    [Fact]
    public async Task An_admin_creates_renames_and_disables_a_category()
    {
        var admin = Admin("cat-admin-crud");

        var created = await admin.PutAsJsonAsync("/api/v1/admin/competition-categories/well-integrity",
            new UpsertCompetitionCategoryRequest("well-integrity", "Well Integrity", "Casing and barrier questions.", "Shield"));
        created.StatusCode.Should().Be(HttpStatusCode.OK);

        var listed = await admin.GetFromJsonAsync<List<CompetitionCategoryDto>>("/api/v1/admin/competition-categories");
        listed!.Should().Contain(c => c.Code == "well-integrity" && c.Name == "Well Integrity" && c.IsEnabled);

        // Renaming and disabling are the same upsert, keyed by code.
        await admin.PutAsJsonAsync("/api/v1/admin/competition-categories/well-integrity",
            new UpsertCompetitionCategoryRequest("well-integrity", "Well Integrity & Barriers", "", "Shield", IsEnabled: false));

        var updated = await admin.GetFromJsonAsync<List<CompetitionCategoryDto>>("/api/v1/admin/competition-categories");
        var category = updated!.Single(c => c.Code == "well-integrity");
        category.Name.Should().Be("Well Integrity & Barriers");
        category.IsEnabled.Should().BeFalse();

        // A disabled category is not offered to anyone choosing one.
        var offered = await admin.GetFromJsonAsync<List<CompetitionCategoryDto>>("/api/v1/competitions/categories");
        offered!.Should().NotContain(c => c.Code == "well-integrity");
    }

    [Fact]
    public async Task A_category_in_use_cannot_be_deleted()
    {
        var admin = Admin("cat-admin-delete");

        await admin.PutAsJsonAsync("/api/v1/admin/competition-categories/temp-cat",
            new UpsertCompetitionCategoryRequest("temp-cat", "Temporary"));

        // Empty, so it goes.
        (await admin.DeleteAsync("/api/v1/admin/competition-categories/temp-cat"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Now one with a competition in it.
        await admin.PutAsJsonAsync("/api/v1/admin/competition-categories/used-cat",
            new UpsertCompetitionCategoryRequest("used-cat", "In Use"));

        var competition = await admin.PostAsJsonAsync("/api/v1/competitions",
            new CreateCompetitionRequest("Categorised", "x", "Company", null, null, 5, "accuracy"));
        var id = (await competition.Content.ReadFromJsonAsync<CompetitionDto>())!.Id;
        (await admin.PutAsJsonAsync($"/api/v1/admin/competitions/{id}/category", new SetCompetitionCategoryRequest("used-cat")))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Deleting would leave that competition pointing at a code nothing resolves.
        var refused = await admin.DeleteAsync("/api/v1/admin/competition-categories/used-cat");
        refused.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await refused.Content.ReadAsStringAsync()).Should().Contain("disable this one instead");
    }

    [Fact]
    public async Task The_category_list_shows_how_many_competitions_use_each()
    {
        var admin = Admin("cat-admin-counts");
        await admin.PutAsJsonAsync("/api/v1/admin/competition-categories/counted",
            new UpsertCompetitionCategoryRequest("counted", "Counted"));

        var competition = await admin.PostAsJsonAsync("/api/v1/competitions",
            new CreateCompetitionRequest("Counted one", "x", "Company", null, null, 5, "accuracy"));
        var id = (await competition.Content.ReadFromJsonAsync<CompetitionDto>())!.Id;
        await admin.PutAsJsonAsync($"/api/v1/admin/competitions/{id}/category", new SetCompetitionCategoryRequest("counted"));

        var listed = await admin.GetFromJsonAsync<List<CompetitionCategoryDto>>("/api/v1/admin/competition-categories");
        listed!.Single(c => c.Code == "counted").CompetitionCount.Should().Be(1);
    }

    [Fact]
    public async Task An_unknown_category_code_is_refused_rather_than_silently_stored()
    {
        var admin = Admin("cat-admin-unknown");
        var competition = await admin.PostAsJsonAsync("/api/v1/competitions",
            new CreateCompetitionRequest("Loose", "x", "Company", null, null, 5, "accuracy"));
        var id = (await competition.Content.ReadFromJsonAsync<CompetitionDto>())!.Id;

        (await admin.PutAsJsonAsync($"/api/v1/admin/competitions/{id}/category", new SetCompetitionCategoryRequest("no-such-category")))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Only_a_platform_admin_may_shape_the_catalogue()
    {
        var member = _factory.CreateClientAs("cat-not-admin", "Employee");

        (await member.GetAsync("/api/v1/admin/competition-categories")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await member.PutAsJsonAsync("/api/v1/admin/competition-categories/sneaky",
            new UpsertCompetitionCategoryRequest("sneaky", "Sneaky"))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_track_can_be_pointed_at_a_competition_and_back_again()
    {
        var admin = Admin("cat-admin-links");

        var competition = await admin.PostAsJsonAsync("/api/v1/competitions",
            new CreateCompetitionRequest("Linked challenge", "x", "Company", null, null, 5, "accuracy"));
        var competitionId = (await competition.Content.ReadFromJsonAsync<CompetitionDto>())!.Id;

        var links = (await admin.GetFromJsonAsync<List<LearningLinkDto>>("/api/v1/admin/learning-links"))!;
        links.Should().NotBeEmpty("the seeded tracks are listed for linking");
        var trackId = links[0].TrackId;
        links[0].RecommendedCompetitionId.Should().BeNull("nothing has ever set this before");

        (await admin.PutAsJsonAsync($"/api/v1/admin/learning-tracks/{trackId}/recommended-competition",
            new SetRecommendedCompetitionRequest(competitionId))).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var after = await admin.GetFromJsonAsync<List<LearningLinkDto>>("/api/v1/admin/learning-links");
        after!.Single(l => l.TrackId == trackId).RecommendedCompetitionId.Should().Be(competitionId);

        // The reverse direction — the competition points back at the track.
        (await admin.PutAsJsonAsync($"/api/v1/admin/competitions/{competitionId}/recommended-track",
            new SetRecommendedTrackRequest(trackId))).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var dto = (await admin.GetFromJsonAsync<CompetitionDto>($"/api/v1/competitions/{competitionId}"))!;
        dto.RecommendedTrackId.Should().Be(trackId);

        // And clearing it works, so a link can be undone.
        (await admin.PutAsJsonAsync($"/api/v1/admin/learning-tracks/{trackId}/recommended-competition",
            new SetRecommendedCompetitionRequest(null))).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await admin.GetFromJsonAsync<List<LearningLinkDto>>("/api/v1/admin/learning-links"))!
            .Single(l => l.TrackId == trackId).RecommendedCompetitionId.Should().BeNull();
    }

    [Fact]
    public async Task Linking_to_something_that_does_not_exist_is_refused()
    {
        var admin = Admin("cat-admin-bad-link");
        var links = await admin.GetFromJsonAsync<List<LearningLinkDto>>("/api/v1/admin/learning-links");

        (await admin.PutAsJsonAsync($"/api/v1/admin/learning-tracks/{links![0].TrackId}/recommended-competition",
            new SetRecommendedCompetitionRequest(Guid.NewGuid()))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
