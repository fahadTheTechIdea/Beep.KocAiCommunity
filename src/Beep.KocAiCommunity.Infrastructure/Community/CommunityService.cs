using System.Text.RegularExpressions;
using Beep.KocAiCommunity.Application.Audit;
using Beep.KocAiCommunity.Application.Authorization;
using Beep.KocAiCommunity.Application.Community;
using Beep.KocAiCommunity.Application.Engagement;
using Beep.KocAiCommunity.Application.Notifications;
using Beep.KocAiCommunity.Application.Organization;
using Beep.KocAiCommunity.Application.Storage;
using Beep.KocAiCommunity.Domain.Common;
using Beep.KocAiCommunity.Domain.Community;
using Beep.KocAiCommunity.Domain.Engagement;
using Beep.KocAiCommunity.Domain.Organization;
using Beep.KocAiCommunity.Domain.Storage;
using Beep.KocAiCommunity.Infrastructure.Engagement;
using Beep.KocAiCommunity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Beep.KocAiCommunity.Infrastructure.Community;

public sealed partial class CommunityService(
    KocDbContext db,
    IOrgDirectory directory,
    IVisibilityEvaluator visibility,
    IEngagementService engagement,
    INotificationService notifications,
    IArtifactService artifacts,
    IAuditEnvelope audit) : ICommunityService
{
    // At most this many distinct users are notified per post — a guard against mass-mention abuse.
    private const int MaxMentionsPerPost = 10;

    [GeneratedRegex(@"@([\p{L}\p{N}._-]+)", RegexOptions.CultureInvariant)]
    private static partial Regex MentionPattern();

    public async Task<Discussion> CreateAsync(string userId, string title, string body, VisibilityScope scope, CancellationToken ct = default)
    {
        var unitId = await directory.ResolveScopeUnitAsync(userId, scope, ct);
        if (scope != VisibilityScope.Company && unitId is null)
        {
            throw new CommunityException("You are not part of an org unit at the '{0}' level.", scope);
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

        await ResolveMentionsAsync(userId, ReactionTargets.Discussion, discussion.Id, body, title, $"/community/{discussion.Id}", ct);
        await AwardSafelyAsync(userId, XpSources.DiscussionCreated, "discussion", discussion.Id, ct);
        return discussion;
    }

    public async Task<IReadOnlyDictionary<string, string>> ResolveDisplayNamesAsync(IReadOnlyCollection<string> userIds, CancellationToken ct = default)
    {
        if (userIds.Count == 0)
        {
            return new Dictionary<string, string>();
        }

        var ids = userIds.Distinct().ToList();
        var names = await db.Set<UserProfile>().AsNoTracking()
            .Where(p => ids.Contains(p.UserId))
            .ToDictionaryAsync(p => p.UserId, p => p.DisplayName, ct);
        return ids.ToDictionary(id => id, id => names.GetValueOrDefault(id, id));
    }

    public async Task<IReadOnlyList<DiscussionView>> BrowseVisibleAsync(string userId, CancellationToken ct = default)
    {
        var all = await db.Set<Discussion>().AsNoTracking()
            .Where(d => !d.IsDeleted)
            .OrderByDescending(d => d.IsPinned).ThenByDescending(d => d.CreatedUtc)
            .ToListAsync(ct);

        var visible = new List<Discussion>(all.Count);
        foreach (var discussion in all)
        {
            if (await CanSeeAsync(userId, discussion, ct))
            {
                visible.Add(discussion);
            }
        }

        var ids = visible.Select(d => d.Id).ToList();
        var replyCounts = await db.Set<DiscussionReply>().AsNoTracking()
            .Where(r => ids.Contains(r.DiscussionId) && !r.IsDeleted)
            .GroupBy(r => r.DiscussionId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        var reactions = await ReactionsForAsync(ReactionTargets.Discussion, ids, userId, ct);

        return visible.Select(d => new DiscussionView(
            d,
            replyCounts.GetValueOrDefault(d.Id),
            reactions.GetValueOrDefault(d.Id) ?? [])).ToList();
    }

    public async Task<DiscussionThreadView?> GetVisibleAsync(string userId, bool isModerator, Guid discussionId, CancellationToken ct = default)
    {
        var discussion = await db.Set<Discussion>().AsNoTracking().FirstOrDefaultAsync(d => d.Id == discussionId && !d.IsDeleted, ct);
        if (discussion is null || !await CanSeeAsync(userId, discussion, ct))
        {
            return null;
        }

        var replies = await db.Set<DiscussionReply>().AsNoTracking()
            .Where(r => r.DiscussionId == discussionId && !r.IsDeleted)
            .OrderBy(r => r.CreatedUtc)
            .ToListAsync(ct);

        var discussionReactions = await SummariesAsync(ReactionTargets.Discussion, discussionId, userId, ct);
        var replyReactions = await ReactionsForAsync(ReactionTargets.Reply, replies.Select(r => r.Id).ToList(), userId, ct);

        var attachments = await db.Set<DiscussionAttachment>().AsNoTracking()
            .Where(a => a.DiscussionId == discussionId && !a.IsDeleted)
            .OrderBy(a => a.CreatedUtc)
            .Select(a => new AttachmentInfo(a.Id, a.FileName, a.SizeBytes, a.UploadedByUserId, a.CreatedUtc))
            .ToListAsync(ct);

        return new DiscussionThreadView(
            new DiscussionView(discussion, replies.Count, discussionReactions),
            replies.Select(r => new ReplyView(r, replyReactions.GetValueOrDefault(r.Id) ?? [])).ToList(),
            attachments,
            isModerator || discussion.AuthorUserId == userId);
    }

    public async Task<DiscussionReply> AddReplyAsync(string userId, Guid discussionId, string body, CancellationToken ct = default)
    {
        var discussion = await db.Set<Discussion>().FirstOrDefaultAsync(d => d.Id == discussionId && !d.IsDeleted, ct)
            ?? throw new CommunityException("Discussion not found.");

        if (!await CanSeeAsync(userId, discussion, ct))
        {
            throw new CommunityException("This discussion is not visible to you.");
        }

        if (discussion.IsLocked)
        {
            throw new CommunityException("This discussion is locked.");
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

        // Notify the thread author of a new reply (unless they replied to themselves).
        if (discussion.AuthorUserId != userId)
        {
            await notifications.NotifyAsync(discussion.AuthorUserId, "discussion-reply",
                "New reply", $"Someone replied to \"{discussion.Title}\".", $"/community/{discussionId}", ct);
        }

        await ResolveMentionsAsync(userId, ReactionTargets.Reply, reply.Id, body, discussion.Title, $"/community/{discussionId}", ct);
        await AwardSafelyAsync(userId, XpSources.DiscussionReplied, "discussion", reply.Id, ct);
        return reply;
    }

    public async Task<IReadOnlyList<ReactionSummary>> ReactAsync(string userId, string targetType, Guid targetId, string emoji, CancellationToken ct = default)
    {
        if (!CommunityEmojis.IsAllowed(emoji))
        {
            throw new CommunityException("That reaction isn't available.");
        }

        if (targetType != ReactionTargets.Discussion && targetType != ReactionTargets.Reply)
        {
            throw new CommunityException("Unknown reaction target.");
        }

        await EnsureTargetVisibleAsync(userId, targetType, targetId, ct);

        var existing = await db.Set<Reaction>()
            .FirstOrDefaultAsync(r => r.TargetType == targetType && r.TargetId == targetId && r.UserId == userId && r.Emoji == emoji, ct);
        if (existing is not null)
        {
            db.Set<Reaction>().Remove(existing);
        }
        else
        {
            db.Set<Reaction>().Add(new Reaction
            {
                TargetType = targetType,
                TargetId = targetId,
                UserId = userId,
                Emoji = emoji,
                CreatedByUserId = userId,
                CreatedUtc = DateTime.UtcNow,
            });
        }

        await db.SaveChangesAsync(ct);
        return await SummariesAsync(targetType, targetId, userId, ct);
    }

    public async Task SetLockAsync(string userId, bool isModerator, Guid discussionId, bool locked, CancellationToken ct = default)
    {
        var d = await RequireModeratableAsync(isModerator, discussionId, ct);
        d.IsLocked = locked;
        d.LastModifiedByUserId = userId;
        d.LastModifiedUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync(new AuditEntry(locked ? "discussion.lock" : "discussion.unlock", "Discussion", discussionId.ToString()), ct);
    }

    public async Task SetPinAsync(string userId, bool isModerator, Guid discussionId, bool pinned, CancellationToken ct = default)
    {
        var d = await RequireModeratableAsync(isModerator, discussionId, ct);
        d.IsPinned = pinned;
        d.LastModifiedByUserId = userId;
        d.LastModifiedUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync(new AuditEntry(pinned ? "discussion.pin" : "discussion.unpin", "Discussion", discussionId.ToString()), ct);
    }

    public async Task DeleteDiscussionAsync(string userId, bool isModerator, Guid discussionId, CancellationToken ct = default)
    {
        var d = await db.Set<Discussion>().FirstOrDefaultAsync(x => x.Id == discussionId && !x.IsDeleted, ct)
            ?? throw new CommunityException("Discussion not found.");
        if (!isModerator && d.AuthorUserId != userId)
        {
            throw new CommunityException("Only the author or a moderator can delete this discussion.");
        }

        d.IsDeleted = true;
        d.LastModifiedByUserId = userId;
        d.LastModifiedUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync(new AuditEntry("discussion.delete", "Discussion", discussionId.ToString()), ct);
    }

    public async Task DeleteReplyAsync(string userId, bool isModerator, Guid replyId, CancellationToken ct = default)
    {
        var r = await db.Set<DiscussionReply>().FirstOrDefaultAsync(x => x.Id == replyId && !x.IsDeleted, ct)
            ?? throw new CommunityException("Reply not found.");
        if (!isModerator && r.AuthorUserId != userId)
        {
            throw new CommunityException("Only the author or a moderator can delete this reply.");
        }

        r.IsDeleted = true;
        r.LastModifiedByUserId = userId;
        r.LastModifiedUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync(new AuditEntry("reply.delete", "DiscussionReply", replyId.ToString()), ct);
    }

    public async Task<IReadOnlyList<MentionCandidate>> SearchMentionCandidatesAsync(string query, int take = 8, CancellationToken ct = default)
    {
        var q = (query ?? string.Empty).Trim();
        var profiles = db.Set<UserProfile>().AsNoTracking();
        var filtered = string.IsNullOrEmpty(q)
            ? profiles.OrderBy(p => p.DisplayName)
            : profiles.Where(p => EF.Functions.Like(p.DisplayName, $"%{q}%")).OrderBy(p => p.DisplayName);

        return await filtered
            .Take(Math.Clamp(take, 1, 25))
            .Select(p => new MentionCandidate(p.UserId, p.DisplayName, p.AvatarIcon))
            .ToListAsync(ct);
    }

    public async Task<AttachmentInfo> AddAttachmentAsync(string userId, Guid discussionId, Stream content, string fileName, string contentType, CancellationToken ct = default)
    {
        var discussion = await db.Set<Discussion>().FirstOrDefaultAsync(d => d.Id == discussionId && !d.IsDeleted, ct)
            ?? throw new CommunityException("Discussion not found.");
        if (!await CanSeeAsync(userId, discussion, ct))
        {
            throw new CommunityException("This discussion is not visible to you.");
        }

        var safeName = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(safeName))
        {
            throw new CommunityException("A file name is required.");
        }

        ArtifactReference reference;
        try
        {
            reference = await artifacts.SaveAsync(content, $"discussions/{discussionId:N}/{safeName}", contentType, KocDataClassification.Internal, ct);
        }
        catch (ArtifactValidationException ex)
        {
            throw new CommunityException(ex.Message);
        }

        var attachment = new DiscussionAttachment
        {
            DiscussionId = discussionId,
            ArtifactReferenceId = reference.Id,
            FileName = safeName,
            SizeBytes = reference.SizeBytes,
            UploadedByUserId = userId,
            CreatedByUserId = userId,
            CreatedUtc = DateTime.UtcNow,
        };
        db.Set<DiscussionAttachment>().Add(attachment);
        await db.SaveChangesAsync(ct);

        return new AttachmentInfo(attachment.Id, attachment.FileName, attachment.SizeBytes, attachment.UploadedByUserId, attachment.CreatedUtc);
    }

    public async Task<AttachmentContent> OpenAttachmentAsync(string userId, Guid attachmentId, CancellationToken ct = default)
    {
        var attachment = await db.Set<DiscussionAttachment>().AsNoTracking().FirstOrDefaultAsync(a => a.Id == attachmentId && !a.IsDeleted, ct)
            ?? throw new CommunityException("Attachment not found.");
        var discussion = await db.Set<Discussion>().AsNoTracking().FirstOrDefaultAsync(d => d.Id == attachment.DiscussionId && !d.IsDeleted, ct)
            ?? throw new CommunityException("Attachment not found.");
        if (!await CanSeeAsync(userId, discussion, ct))
        {
            throw new CommunityException("This attachment is not visible to you.");
        }

        var reference = await db.Set<ArtifactReference>().AsNoTracking().FirstOrDefaultAsync(r => r.Id == attachment.ArtifactReferenceId, ct)
            ?? throw new CommunityException("Attachment content is missing.");
        var stream = await artifacts.OpenReadAsync(attachment.ArtifactReferenceId, ct);
        return new AttachmentContent(stream, attachment.FileName, reference.ContentType);
    }

    private async Task<Discussion> RequireModeratableAsync(bool isModerator, Guid discussionId, CancellationToken ct)
    {
        if (!isModerator)
        {
            throw new CommunityException("You don't have moderator permission.");
        }

        return await db.Set<Discussion>().FirstOrDefaultAsync(d => d.Id == discussionId && !d.IsDeleted, ct)
            ?? throw new CommunityException("Discussion not found.");
    }

    private async Task EnsureTargetVisibleAsync(string userId, string targetType, Guid targetId, CancellationToken ct)
    {
        var discussionId = targetType == ReactionTargets.Reply
            ? await db.Set<DiscussionReply>().AsNoTracking().Where(r => r.Id == targetId && !r.IsDeleted).Select(r => (Guid?)r.DiscussionId).FirstOrDefaultAsync(ct)
            : targetId;
        if (discussionId is null)
        {
            throw new CommunityException("That item no longer exists.");
        }

        var discussion = await db.Set<Discussion>().AsNoTracking().FirstOrDefaultAsync(d => d.Id == discussionId && !d.IsDeleted, ct);
        if (discussion is null || !await CanSeeAsync(userId, discussion, ct))
        {
            throw new CommunityException("This item is not visible to you.");
        }
    }

    private async Task<Dictionary<Guid, IReadOnlyList<ReactionSummary>>> ReactionsForAsync(string targetType, IReadOnlyList<Guid> targetIds, string userId, CancellationToken ct)
    {
        if (targetIds.Count == 0)
        {
            return [];
        }

        var rows = await db.Set<Reaction>().AsNoTracking()
            .Where(r => r.TargetType == targetType && targetIds.Contains(r.TargetId))
            .Select(r => new { r.TargetId, r.Emoji, r.UserId })
            .ToListAsync(ct);

        return rows
            .GroupBy(r => r.TargetId)
            .ToDictionary(g => g.Key, g => Summarize(g.Select(x => (x.Emoji, x.UserId)), userId));
    }

    private async Task<IReadOnlyList<ReactionSummary>> SummariesAsync(string targetType, Guid targetId, string userId, CancellationToken ct)
    {
        var rows = await db.Set<Reaction>().AsNoTracking()
            .Where(r => r.TargetType == targetType && r.TargetId == targetId)
            .Select(r => new { r.Emoji, r.UserId })
            .ToListAsync(ct);
        return Summarize(rows.Select(x => (x.Emoji, x.UserId)), userId);
    }

    private static IReadOnlyList<ReactionSummary> Summarize(IEnumerable<(string Emoji, string UserId)> rows, string userId)
    {
        var byEmoji = rows.GroupBy(r => r.Emoji)
            .ToDictionary(g => g.Key, g => (Count: g.Count(), Mine: g.Any(x => x.UserId == userId)));

        // Preserve the curated emoji order; only include emojis that have at least one reaction.
        return CommunityEmojis.Allowed
            .Where(byEmoji.ContainsKey)
            .Select(e => new ReactionSummary(e, byEmoji[e].Count, byEmoji[e].Mine))
            .ToList();
    }

    private async Task ResolveMentionsAsync(string byUserId, string sourceType, Guid sourceId, string body, string title, string linkUrl, CancellationToken ct)
    {
        var tokens = MentionPattern().Matches(body)
            .Select(m => m.Groups[1].Value)
            .Where(t => t.Length > 0)
            .Select(Normalize)
            .Distinct()
            .ToList();
        if (tokens.Count == 0)
        {
            return;
        }

        // Resolve tokens to KOC profiles by normalized display name or exact user id.
        var candidates = await db.Set<UserProfile>().AsNoTracking()
            .Select(p => new { p.UserId, p.DisplayName })
            .ToListAsync(ct);

        var resolved = candidates
            .Where(c => tokens.Contains(Normalize(c.DisplayName)) || tokens.Contains(Normalize(c.UserId)))
            .Select(c => c.UserId)
            .Where(id => id != byUserId)          // don't notify yourself
            .Distinct()
            .Take(MaxMentionsPerPost)              // guard against mass-mention abuse
            .ToList();
        if (resolved.Count == 0)
        {
            return;
        }

        foreach (var mentionedUserId in resolved)
        {
            db.Set<Mention>().Add(new Mention
            {
                SourceType = sourceType,
                SourceId = sourceId,
                MentionedUserId = mentionedUserId,
                ByUserId = byUserId,
                CreatedByUserId = byUserId,
                CreatedUtc = DateTime.UtcNow,
            });
        }

        await db.SaveChangesAsync(ct);
        await notifications.NotifyManyAsync(resolved, "mention", "You were mentioned",
            $"You were mentioned in \"{title}\".", linkUrl, ct);
    }

    private static string Normalize(string value) =>
        new string(value.Where(c => !char.IsWhiteSpace(c)).ToArray()).ToLowerInvariant();

    // An author always sees their own thread, whatever its scope. The length check keeps that shortcut
    // from firing for an anonymous reader, whose id is the empty string: a row that somehow carried an
    // empty author would otherwise be visible to the whole internet.
    private async Task<bool> CanSeeAsync(string userId, Discussion discussion, CancellationToken ct) =>
        (userId.Length > 0 && discussion.AuthorUserId == userId)
        || await visibility.CanSeeAsync(userId, discussion.VisibilityScope, discussion.VisibilityOrgUnitId, ct);

    // Barrels are a side effect of collaborating: never let an award failure fail the post.
    private async Task AwardSafelyAsync(string userId, string source, string refType, Guid refId, CancellationToken ct)
    {
        try
        {
            await engagement.AwardXpAsync(userId, source, refType, refId, ct);
        }
        catch (Exception)
        {
            // Swallow — the discussion/reply already committed.
        }
    }
}
