using System.Net;
using System.Net.Http.Json;
using Beep.KocAiCommunity.Contracts.Community;
using FluentAssertions;
using Xunit;

namespace Beep.KocAiCommunity.IntegrationTests;

public class DiscussionEndpointsTests(KocApiFactory factory) : IClassFixture<KocApiFactory>
{
    private readonly KocApiFactory _factory = factory;

    [Fact]
    public async Task Group_scoped_discussion_is_visible_within_the_group_and_supports_replies()
    {
        var manager = _factory.CreateClientAs("mgr1", "Manager");
        var created = await (await manager.PostAsJsonAsync("/api/v1/discussions",
            new CreateDiscussionRequest("Reservoir modelling tips", "How do you handle PI gaps?", "Group")))
            .Content.ReadFromJsonAsync<DiscussionDto>();
        created!.Scope.Should().Be("Group");

        // A group member can see it and reply.
        var member = _factory.CreateClientAs("emp1", "Employee");
        var list = (await member.GetFromJsonAsync<List<DiscussionDto>>("/api/v1/discussions"))!;
        list.Should().Contain(d => d.Id == created.Id);

        (await member.PostAsJsonAsync($"/api/v1/discussions/{created.Id}/replies", new CreateReplyRequest("Impute with last-known value.")))
            .EnsureSuccessStatusCode();

        var detail = (await member.GetFromJsonAsync<DiscussionDetailDto>($"/api/v1/discussions/{created.Id}"))!;
        detail.Replies.Should().ContainSingle(r => r.AuthorUserId == "emp1");

        // An outsider cannot see it and cannot reply.
        var outsider = _factory.CreateClientAs("empOther", "Employee");
        (await outsider.GetFromJsonAsync<List<DiscussionDto>>("/api/v1/discussions"))!.Should().NotContain(d => d.Id == created.Id);
        (await outsider.GetAsync($"/api/v1/discussions/{created.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await outsider.PostAsJsonAsync($"/api/v1/discussions/{created.Id}/replies", new CreateReplyRequest("hi")))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Anyone_can_read_the_community_but_taking_part_needs_an_account()
    {
        // The same rule as the learning catalogue: what the platform is for is visible before anyone
        // commits to an account. Writing is what needs somebody to attribute it to.
        var author = _factory.CreateClientAs("community-author", "Employee");
        var created = (await (await author.PostAsJsonAsync("/api/v1/discussions",
            new CreateDiscussionRequest("Open to read", "Anyone should be able to read this.", "Company")))
            .Content.ReadFromJsonAsync<DiscussionDto>())!;

        var guest = _factory.CreateClientAs(sub: null);

        var list = (await guest.GetFromJsonAsync<List<DiscussionDto>>("/api/v1/discussions"))!;
        list.Should().Contain(d => d.Id == created.Id);

        var thread = (await guest.GetFromJsonAsync<DiscussionDetailDto>($"/api/v1/discussions/{created.Id}"))!;
        thread.Body.Should().Be("Anyone should be able to read this.");
        thread.CanModerate.Should().BeFalse("a reader with no account moderates nothing");

        // Every way of taking part still needs a person.
        (await guest.PostAsJsonAsync("/api/v1/discussions", new CreateDiscussionRequest("Mine", "b", "Company")))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await guest.PostAsJsonAsync($"/api/v1/discussions/{created.Id}/replies", new CreateReplyRequest("hi")))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await guest.PostAsJsonAsync($"/api/v1/discussions/{created.Id}/react", new ReactRequest("👍")))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await guest.DeleteAsync($"/api/v1/discussions/{created.Id}"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Opening_the_community_up_does_not_open_the_narrower_discussions()
    {
        // The gate that moved was authentication, not visibility. A group-scoped thread must stay
        // invisible to a reader with no org membership — which is exactly what an anonymous caller is.
        var manager = _factory.CreateClientAs("mgr-private", "Manager");
        var group = (await (await manager.PostAsJsonAsync("/api/v1/discussions",
            new CreateDiscussionRequest("Team only", "Not for the internet.", "Group")))
            .Content.ReadFromJsonAsync<DiscussionDto>())!;

        var guest = _factory.CreateClientAs(sub: null);

        (await guest.GetFromJsonAsync<List<DiscussionDto>>("/api/v1/discussions"))!
            .Should().NotContain(d => d.Id == group.Id);
        (await guest.GetAsync($"/api/v1/discussions/{group.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task The_staff_directory_is_not_searchable_without_an_account()
    {
        // Mention candidates are colleagues' names. Reading a thread is public; enumerating who works
        // here is not, and the only caller who needs it is one already composing a post.
        var guest = _factory.CreateClientAs(sub: null);

        (await guest.GetAsync("/api/v1/community/mention-candidates?q=a"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
