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
}
