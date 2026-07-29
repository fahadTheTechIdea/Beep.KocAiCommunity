using Beep.KocAiCommunity.Application.Localization;
using Beep.KocAiCommunity.Domain.Community;
using Beep.KocAiCommunity.Domain.Organization;

namespace Beep.KocAiCommunity.Application.Community;

/// <summary>Raised when a discussion action is not permitted (visibility, missing thread, locked).</summary>
public sealed class CommunityException : Exception, IUserFacingMessage
{
    /// <summary>
    /// A message the member will read. Pass the English with <c>{0}</c> placeholders and the values
    /// separately — never an interpolated string, or the sentence cannot be looked up for translation.
    /// </summary>
    public CommunityException(string template, params object[] args)
        : base(UserFacingMessage.Format(template, args))
    {
        Template = template;
        TemplateArgs = args;
    }

    public string Template { get; }

    public object[] TemplateArgs { get; }
}

/// <summary>The curated emoji reactions employees can leave on discussions and replies.</summary>
public static class CommunityEmojis
{
    public static readonly IReadOnlyList<string> Allowed = ["👍", "❤️", "🎉", "💡", "🚀", "✅"];

    public static bool IsAllowed(string emoji) => Allowed.Contains(emoji);
}

/// <summary>Reaction target kinds.</summary>
public static class ReactionTargets
{
    public const string Discussion = "discussion";
    public const string Reply = "reply";
}

/// <summary>An emoji reaction tally on a target, with whether the caller reacted.</summary>
public sealed record ReactionSummary(string Emoji, int Count, bool Mine);

/// <summary>A discussion with its reply count and reaction tallies, for listing.</summary>
public sealed record DiscussionView(Discussion Discussion, int ReplyCount, IReadOnlyList<ReactionSummary> Reactions);

/// <summary>A reply with its reaction tallies.</summary>
public sealed record ReplyView(DiscussionReply Reply, IReadOnlyList<ReactionSummary> Reactions);

/// <summary>An attachment's metadata (not its bytes).</summary>
public sealed record AttachmentInfo(Guid Id, string FileName, long SizeBytes, string UploadedByUserId, DateTime CreatedUtc);

/// <summary>A full thread view: the discussion, its replies, attachments, and the caller's moderation right.</summary>
public sealed record DiscussionThreadView(
    DiscussionView Discussion,
    IReadOnlyList<ReplyView> Replies,
    IReadOnlyList<AttachmentInfo> Attachments,
    bool CanModerate);

/// <summary>A mention autocomplete candidate — a KOC user who can be @mentioned.</summary>
public sealed record MentionCandidate(string UserId, string DisplayName, string AvatarIcon);

/// <summary>An opened attachment: its bytes plus the metadata needed to serve it.</summary>
public sealed record AttachmentContent(Stream Content, string FileName, string ContentType);

public interface ICommunityService
{
    Task<Discussion> CreateAsync(string userId, string title, string body, VisibilityScope scope, CancellationToken ct = default);
    Task<IReadOnlyList<DiscussionView>> BrowseVisibleAsync(string userId, CancellationToken ct = default);
    Task<DiscussionThreadView?> GetVisibleAsync(string userId, bool isModerator, Guid discussionId, CancellationToken ct = default);
    Task<DiscussionReply> AddReplyAsync(string userId, Guid discussionId, string body, CancellationToken ct = default);

    /// <summary>Toggles the caller's emoji reaction on a discussion or reply; returns the new tallies.</summary>
    Task<IReadOnlyList<ReactionSummary>> ReactAsync(string userId, string targetType, Guid targetId, string emoji, CancellationToken ct = default);

    // Moderation (moderator = PlatformAdmin or an org-unit leader). All actions are audited.
    Task SetLockAsync(string userId, bool isModerator, Guid discussionId, bool locked, CancellationToken ct = default);
    Task SetPinAsync(string userId, bool isModerator, Guid discussionId, bool pinned, CancellationToken ct = default);
    Task DeleteDiscussionAsync(string userId, bool isModerator, Guid discussionId, CancellationToken ct = default);
    Task DeleteReplyAsync(string userId, bool isModerator, Guid replyId, CancellationToken ct = default);

    // Mentions.
    Task<IReadOnlyList<MentionCandidate>> SearchMentionCandidatesAsync(string query, int take = 8, CancellationToken ct = default);

    /// <summary>Resolves community display names for a set of user ids (falls back to the id).</summary>
    Task<IReadOnlyDictionary<string, string>> ResolveDisplayNamesAsync(IReadOnlyCollection<string> userIds, CancellationToken ct = default);

    // Attachments (classification inherited as Internal; malware scanning is a documented follow-up).
    Task<AttachmentInfo> AddAttachmentAsync(string userId, Guid discussionId, Stream content, string fileName, string contentType, CancellationToken ct = default);
    Task<AttachmentContent> OpenAttachmentAsync(string userId, Guid attachmentId, CancellationToken ct = default);
}
