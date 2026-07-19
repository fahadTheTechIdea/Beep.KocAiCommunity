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
                return Results.Ok(new DiscussionDto(discussion.Id, discussion.Title, discussion.Body,
                    discussion.VisibilityScope.ToString(), discussion.AuthorUserId, discussion.CreatedUtc, 0, false, false, []));
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
            return Results.Ok(visible.Select(ToDto).ToList());
        })
        .WithName("BrowseDiscussions")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        group.MapGet("/discussions/{id:guid}", async (Guid id, IKocCurrentUser me, ICommunityService svc, CancellationToken ct) =>
        {
            var thread = await svc.GetVisibleAsync(me.UserId!, IsModerator(me), id, ct);
            if (thread is null)
            {
                return Results.NotFound();
            }

            var d = thread.Discussion.Discussion;
            return Results.Ok(new DiscussionDetailDto(
                d.Id, d.Title, d.Body, d.VisibilityScope.ToString(), d.AuthorUserId, d.CreatedUtc,
                d.IsPinned, d.IsLocked, thread.CanModerate,
                ToReactions(thread.Discussion.Reactions),
                [.. thread.Replies.Select(r => new ReplyDto(r.Reply.Id, r.Reply.AuthorUserId, r.Reply.Body, r.Reply.CreatedUtc, ToReactions(r.Reactions)))],
                [.. thread.Attachments.Select(a => new AttachmentDto(a.Id, a.FileName, a.SizeBytes, a.UploadedByUserId, a.CreatedUtc))]));
        })
        .WithName("GetDiscussion")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        group.MapPost("/discussions/{id:guid}/replies", async (Guid id, CreateReplyRequest req, IKocCurrentUser me, ICommunityService svc, CancellationToken ct) =>
        {
            try
            {
                var reply = await svc.AddReplyAsync(me.UserId!, id, req.Body, ct);
                return Results.Ok(new ReplyDto(reply.Id, reply.AuthorUserId, reply.Body, reply.CreatedUtc, []));
            }
            catch (CommunityException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("AddReply")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        group.MapPost("/discussions/{id:guid}/react", (Guid id, ReactRequest req, IKocCurrentUser me, ICommunityService svc, CancellationToken ct) =>
            React(svc, me.UserId!, ReactionTargets.Discussion, id, req.Emoji, ct))
        .WithName("ReactToDiscussion")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        group.MapPost("/discussions/{id:guid}/replies/{replyId:guid}/react", (Guid id, Guid replyId, ReactRequest req, IKocCurrentUser me, ICommunityService svc, CancellationToken ct) =>
            React(svc, me.UserId!, ReactionTargets.Reply, replyId, req.Emoji, ct))
        .WithName("ReactToReply")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        group.MapPost("/discussions/{id:guid}/lock", (Guid id, IKocCurrentUser me, ICommunityService svc, CancellationToken ct) =>
            Moderate(() => svc.SetLockAsync(me.UserId!, IsModerator(me), id, true, ct)))
        .WithName("LockDiscussion").RequireAuthorization(KocPolicies.RequireEmployee);

        group.MapPost("/discussions/{id:guid}/unlock", (Guid id, IKocCurrentUser me, ICommunityService svc, CancellationToken ct) =>
            Moderate(() => svc.SetLockAsync(me.UserId!, IsModerator(me), id, false, ct)))
        .WithName("UnlockDiscussion").RequireAuthorization(KocPolicies.RequireEmployee);

        group.MapPost("/discussions/{id:guid}/pin", (Guid id, IKocCurrentUser me, ICommunityService svc, CancellationToken ct) =>
            Moderate(() => svc.SetPinAsync(me.UserId!, IsModerator(me), id, true, ct)))
        .WithName("PinDiscussion").RequireAuthorization(KocPolicies.RequireEmployee);

        group.MapPost("/discussions/{id:guid}/unpin", (Guid id, IKocCurrentUser me, ICommunityService svc, CancellationToken ct) =>
            Moderate(() => svc.SetPinAsync(me.UserId!, IsModerator(me), id, false, ct)))
        .WithName("UnpinDiscussion").RequireAuthorization(KocPolicies.RequireEmployee);

        group.MapDelete("/discussions/{id:guid}", (Guid id, IKocCurrentUser me, ICommunityService svc, CancellationToken ct) =>
            Moderate(() => svc.DeleteDiscussionAsync(me.UserId!, IsModerator(me), id, ct)))
        .WithName("DeleteDiscussion").RequireAuthorization(KocPolicies.RequireEmployee);

        group.MapDelete("/discussions/{id:guid}/replies/{replyId:guid}", (Guid id, Guid replyId, IKocCurrentUser me, ICommunityService svc, CancellationToken ct) =>
            Moderate(() => svc.DeleteReplyAsync(me.UserId!, IsModerator(me), replyId, ct)))
        .WithName("DeleteReply").RequireAuthorization(KocPolicies.RequireEmployee);

        group.MapGet("/community/mention-candidates", async (string? q, IKocCurrentUser me, ICommunityService svc, CancellationToken ct) =>
        {
            var candidates = await svc.SearchMentionCandidatesAsync(q ?? string.Empty, 8, ct);
            return Results.Ok(candidates.Select(c => new MentionCandidateDto(c.UserId, c.DisplayName, c.AvatarIcon)).ToList());
        })
        .WithName("MentionCandidates")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        group.MapPost("/discussions/{id:guid}/attachments", async (Guid id, IFormFile file, IKocCurrentUser me, ICommunityService svc, CancellationToken ct) =>
        {
            try
            {
                await using var stream = file.OpenReadStream();
                var info = await svc.AddAttachmentAsync(me.UserId!, id, stream, file.FileName, file.ContentType ?? "application/octet-stream", ct);
                return Results.Ok(new AttachmentDto(info.Id, info.FileName, info.SizeBytes, info.UploadedByUserId, info.CreatedUtc));
            }
            catch (CommunityException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("AddAttachment")
        .RequireAuthorization(KocPolicies.RequireEmployee)
        .DisableAntiforgery();

        group.MapGet("/community/attachments/{attachmentId:guid}", async (Guid attachmentId, IKocCurrentUser me, ICommunityService svc, CancellationToken ct) =>
        {
            try
            {
                var content = await svc.OpenAttachmentAsync(me.UserId!, attachmentId, ct);
                return Results.File(content.Content, content.ContentType, content.FileName);
            }
            catch (CommunityException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("DownloadAttachment")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        return group;
    }

    // A moderator is a platform admin or an org-unit leader.
    private static bool IsModerator(IKocCurrentUser me) => me.IsInRole(KocRoles.PlatformAdmin) || me.LedOrgUnitId is not null;

    private static async Task<IResult> React(ICommunityService svc, string userId, string targetType, Guid targetId, string emoji, CancellationToken ct)
    {
        try
        {
            var reactions = await svc.ReactAsync(userId, targetType, targetId, emoji, ct);
            return Results.Ok(ToReactions(reactions));
        }
        catch (CommunityException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static async Task<IResult> Moderate(Func<Task> action)
    {
        try
        {
            await action();
            return Results.NoContent();
        }
        catch (CommunityException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static DiscussionDto ToDto(DiscussionView v) =>
        new(v.Discussion.Id, v.Discussion.Title, v.Discussion.Body, v.Discussion.VisibilityScope.ToString(),
            v.Discussion.AuthorUserId, v.Discussion.CreatedUtc, v.ReplyCount, v.Discussion.IsPinned, v.Discussion.IsLocked, ToReactions(v.Reactions));

    private static List<ReactionDto> ToReactions(IReadOnlyList<ReactionSummary> reactions) =>
        [.. reactions.Select(r => new ReactionDto(r.Emoji, r.Count, r.Mine))];
}
