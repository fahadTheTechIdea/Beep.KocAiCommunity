using Beep.KocAiCommunity.Application.Community;
using Beep.KocAiCommunity.Application.Security;
using Beep.KocAiCommunity.Contracts.Community;
using Beep.KocAiCommunity.Domain.Community;
using Beep.KocAiCommunity.Domain.Organization;

namespace Beep.KocAiCommunity.Api.Endpoints;

public static class DiscussionEndpoints
{
    public static RouteGroupBuilder MapDiscussionEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/discussions", async (CreateDiscussionRequest req, IKocCurrentUser me, ICommunityService svc, CancellationToken ct) =>
        {
            if (!Enum.TryParse<VisibilityScope>(req.Scope, ignoreCase: true, out var scope))
            {
                return Results.BadRequest(new { error = $"Unknown visibility scope '{req.Scope}'." });
            }

            try
            {
                var discussion = await svc.CreateAsync(me.UserId!, req.Title, req.Body, scope, ct);
                return Results.Ok(ToDto(discussion, 0));
            }
            catch (CommunityException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("CreateDiscussion")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        group.MapGet("/discussions", async (IKocCurrentUser me, ICommunityService svc, CancellationToken ct) =>
        {
            var visible = await svc.BrowseVisibleAsync(me.UserId!, ct);
            return Results.Ok(visible.Select(d => ToDto(d, 0)).ToList());
        })
        .WithName("BrowseDiscussions")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        group.MapGet("/discussions/{id:guid}", async (Guid id, IKocCurrentUser me, ICommunityService svc, CancellationToken ct) =>
        {
            var thread = await svc.GetVisibleAsync(me.UserId!, id, ct);
            if (thread is null)
            {
                return Results.NotFound();
            }

            var d = thread.Discussion;
            return Results.Ok(new DiscussionDetailDto(
                d.Id, d.Title, d.Body, d.VisibilityScope.ToString(), d.AuthorUserId, d.CreatedUtc,
                [.. thread.Replies.Select(r => new ReplyDto(r.Id, r.AuthorUserId, r.Body, r.CreatedUtc))]));
        })
        .WithName("GetDiscussion")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        group.MapPost("/discussions/{id:guid}/replies", async (Guid id, CreateReplyRequest req, IKocCurrentUser me, ICommunityService svc, CancellationToken ct) =>
        {
            try
            {
                var reply = await svc.AddReplyAsync(me.UserId!, id, req.Body, ct);
                return Results.Ok(new ReplyDto(reply.Id, reply.AuthorUserId, reply.Body, reply.CreatedUtc));
            }
            catch (CommunityException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("AddReply")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        return group;
    }

    private static DiscussionDto ToDto(Discussion d, int replyCount) =>
        new(d.Id, d.Title, d.Body, d.VisibilityScope.ToString(), d.AuthorUserId, d.CreatedUtc, replyCount);
}
