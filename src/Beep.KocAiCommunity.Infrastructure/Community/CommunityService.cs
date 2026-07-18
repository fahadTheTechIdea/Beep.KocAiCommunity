using Beep.KocAiCommunity.Application.Authorization;
using Beep.KocAiCommunity.Application.Community;
using Beep.KocAiCommunity.Application.Organization;
using Beep.KocAiCommunity.Domain.Community;
using Beep.KocAiCommunity.Domain.Organization;
using Beep.KocAiCommunity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Beep.KocAiCommunity.Infrastructure.Community;

public sealed class CommunityService(
    KocDbContext db,
    IOrgDirectory directory,
    IVisibilityEvaluator visibility) : ICommunityService
{
    public async Task<Discussion> CreateAsync(string userId, string title, string body, VisibilityScope scope, CancellationToken ct = default)
    {
        var unitId = await directory.ResolveScopeUnitAsync(userId, scope, ct);
        if (scope != VisibilityScope.Company && unitId is null)
        {
            throw new CommunityException($"You are not part of an org unit at the '{scope}' level.");
        }

        var discussion = new Discussion
        {
            Title = title,
            Body = body,
            AuthorUserId = userId,
            VisibilityScope = scope,
            VisibilityOrgUnitId = unitId ?? Guid.Empty,
            CreatedByUserId = userId,
            CreatedUtc = DateTime.UtcNow,
        };

        db.Set<Discussion>().Add(discussion);
        await db.SaveChangesAsync(ct);
        return discussion;
    }

    public async Task<IReadOnlyList<Discussion>> BrowseVisibleAsync(string userId, CancellationToken ct = default)
    {
        var all = await db.Set<Discussion>().AsNoTracking()
            .OrderByDescending(d => d.CreatedUtc)
            .ToListAsync(ct);

        var visible = new List<Discussion>(all.Count);
        foreach (var discussion in all)
        {
            if (await CanSeeAsync(userId, discussion, ct))
            {
                visible.Add(discussion);
            }
        }

        return visible;
    }

    public async Task<DiscussionThread?> GetVisibleAsync(string userId, Guid discussionId, CancellationToken ct = default)
    {
        var discussion = await db.Set<Discussion>().AsNoTracking().FirstOrDefaultAsync(d => d.Id == discussionId, ct);
        if (discussion is null || !await CanSeeAsync(userId, discussion, ct))
        {
            return null;
        }

        var replies = await db.Set<DiscussionReply>().AsNoTracking()
            .Where(r => r.DiscussionId == discussionId)
            .OrderBy(r => r.CreatedUtc)
            .ToListAsync(ct);

        return new DiscussionThread(discussion, replies);
    }

    public async Task<DiscussionReply> AddReplyAsync(string userId, Guid discussionId, string body, CancellationToken ct = default)
    {
        var discussion = await db.Set<Discussion>().FirstOrDefaultAsync(d => d.Id == discussionId, ct)
            ?? throw new CommunityException("Discussion not found.");

        if (!await CanSeeAsync(userId, discussion, ct))
        {
            throw new CommunityException("This discussion is not visible to you.");
        }

        var reply = new DiscussionReply
        {
            DiscussionId = discussionId,
            AuthorUserId = userId,
            Body = body,
            CreatedByUserId = userId,
            CreatedUtc = DateTime.UtcNow,
        };

        db.Set<DiscussionReply>().Add(reply);
        await db.SaveChangesAsync(ct);
        return reply;
    }

    private async Task<bool> CanSeeAsync(string userId, Discussion discussion, CancellationToken ct) =>
        discussion.AuthorUserId == userId
        || await visibility.CanSeeAsync(userId, discussion.VisibilityScope, discussion.VisibilityOrgUnitId, ct);
}
