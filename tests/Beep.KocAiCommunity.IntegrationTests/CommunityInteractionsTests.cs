using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Beep.KocAiCommunity.Contracts.Community;
using Beep.KocAiCommunity.Contracts.Notifications;
using FluentAssertions;
using Xunit;

namespace Beep.KocAiCommunity.IntegrationTests;

public class CommunityInteractionsTests(KocApiFactory factory) : IClassFixture<KocApiFactory>
{
    private readonly KocApiFactory _factory = factory;

    [Fact]
    public async Task Emoji_reactions_toggle_on_discussions_and_replies()
    {
        var mgr = _factory.CreateClientAs("mgr1", "Manager");
        var d = await CreateAsync(mgr, "Reactions thread", "Body", "Group");

        // React 👍 → tallied with mine=true.
        var after = await (await mgr.PostAsJsonAsync($"/api/v1/discussions/{d.Id}/react", new ReactRequest("👍")))
            .Content.ReadFromJsonAsync<List<ReactionDto>>();
        after!.Should().ContainSingle(r => r.Emoji == "👍" && r.Count == 1 && r.Mine);

        // React 👍 again → toggled off.
        var toggled = await (await mgr.PostAsJsonAsync($"/api/v1/discussions/{d.Id}/react", new ReactRequest("👍")))
            .Content.ReadFromJsonAsync<List<ReactionDto>>();
        toggled!.Should().NotContain(r => r.Emoji == "👍");

        // A disallowed emoji is rejected.
        (await mgr.PostAsJsonAsync($"/api/v1/discussions/{d.Id}/react", new ReactRequest("x")))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Reactions also work on replies.
        var reply = await (await mgr.PostAsJsonAsync($"/api/v1/discussions/{d.Id}/replies", new CreateReplyRequest("A reply")))
            .Content.ReadFromJsonAsync<ReplyDto>();
        var replyReactions = await (await mgr.PostAsJsonAsync($"/api/v1/discussions/{d.Id}/replies/{reply!.Id}/react", new ReactRequest("🎉")))
            .Content.ReadFromJsonAsync<List<ReactionDto>>();
        replyReactions!.Should().ContainSingle(r => r.Emoji == "🎉" && r.Mine);
    }

    [Fact]
    public async Task Moderator_can_lock_and_pin_but_a_regular_employee_cannot()
    {
        // The leader path (LedOrgUnitId) relies on a sign-in claim the test harness doesn't set, so
        // the PlatformAdmin moderator path is what's exercised here.
        var mod = _factory.CreateClientAs("mgr1", "Manager", "PlatformAdmin");
        var emp = _factory.CreateClientAs("emp1", "Employee");  // neither admin nor leader
        var d = await CreateAsync(mod, "Moderation thread", "Body", "Group");

        // A non-moderator cannot lock.
        (await emp.PostAsync($"/api/v1/discussions/{d.Id}/lock", null)).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // The moderator locks and pins.
        (await mod.PostAsync($"/api/v1/discussions/{d.Id}/lock", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await mod.PostAsync($"/api/v1/discussions/{d.Id}/pin", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        // A locked thread rejects new replies.
        (await emp.PostAsJsonAsync($"/api/v1/discussions/{d.Id}/replies", new CreateReplyRequest("late")))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var detail = (await mod.GetFromJsonAsync<DiscussionDetailDto>($"/api/v1/discussions/{d.Id}"))!;
        detail.IsLocked.Should().BeTrue();
        detail.IsPinned.Should().BeTrue();
        detail.CanModerate.Should().BeTrue();
    }

    [Fact]
    public async Task Author_can_delete_their_own_discussion()
    {
        var emp = _factory.CreateClientAs("emp1", "Employee");
        var d = await CreateAsync(emp, "Delete me", "Body", "Team");

        (await emp.DeleteAsync($"/api/v1/discussions/{d.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await emp.GetAsync($"/api/v1/discussions/{d.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Mentioning_a_KOC_user_notifies_them_and_autocomplete_finds_them()
    {
        // emp1 posts once so their profile exists (created on first engagement) and is mentionable.
        var emp = _factory.CreateClientAs("emp1", "Employee");
        await CreateAsync(emp, "Presence", "hi", "Team");

        // Autocomplete suggests emp1.
        var candidates = await emp.GetFromJsonAsync<List<MentionCandidateDto>>("/api/v1/community/mention-candidates?q=emp1");
        candidates.Should().Contain(c => c.UserId == "emp1");

        // mgr1 mentions @emp1 in a group thread emp1 can see.
        var mgr = _factory.CreateClientAs("mgr1", "Manager");
        await CreateAsync(mgr, "Mention thread", "Great point @emp1 — take a look.", "Group");

        var notifications = await emp.GetFromJsonAsync<List<NotificationDto>>("/api/v1/notifications");
        notifications.Should().Contain(n => n.Type == "mention");
    }

    [Fact]
    public async Task Attachment_uploads_lists_and_downloads()
    {
        var mgr = _factory.CreateClientAs("mgr1", "Manager");
        var d = await CreateAsync(mgr, "Attachment thread", "Body", "Group");

        const string content = "col1,col2\n1,2\n";
        using var form = new MultipartFormDataContent();
        var part = new StringContent(content);
        part.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        form.Add(part, "file", "sample.csv");
        var uploaded = await (await mgr.PostAsync($"/api/v1/discussions/{d.Id}/attachments", form))
            .Content.ReadFromJsonAsync<AttachmentDto>();
        uploaded!.FileName.Should().Be("sample.csv");

        var detail = (await mgr.GetFromJsonAsync<DiscussionDetailDto>($"/api/v1/discussions/{d.Id}"))!;
        detail.Attachments.Should().ContainSingle(a => a.Id == uploaded.Id);

        var download = await mgr.GetAsync($"/api/v1/community/attachments/{uploaded.Id}");
        download.StatusCode.Should().Be(HttpStatusCode.OK);
        (await download.Content.ReadAsStringAsync()).Should().Be(content);
    }

    private async Task<DiscussionDto> CreateAsync(HttpClient client, string title, string body, string scope) =>
        (await (await client.PostAsJsonAsync("/api/v1/discussions", new CreateDiscussionRequest(title, body, scope)))
            .Content.ReadFromJsonAsync<DiscussionDto>())!;
}
