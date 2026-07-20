namespace Beep.KocAiCommunity.Contracts.Community;

public sealed record CreateDiscussionRequest(string Title, string Body, string Scope);

public sealed record CreateReplyRequest(string Body);

/// <summary>An emoji reaction tally; <paramref name="Mine"/> is whether the caller reacted with it.</summary>
public sealed record ReactionDto(string Emoji, int Count, bool Mine);

public sealed record ReactRequest(string Emoji);

public sealed record ReplyDto(Guid Id, string AuthorUserId, string AuthorDisplayName, string Body, DateTime CreatedUtc, IReadOnlyList<ReactionDto> Reactions);

public sealed record DiscussionDto(
    Guid Id, string Title, string Body, string Scope, string AuthorUserId, string AuthorDisplayName, DateTime CreatedUtc,
    int ReplyCount, bool IsPinned, bool IsLocked, IReadOnlyList<ReactionDto> Reactions);

public sealed record AttachmentDto(Guid Id, string FileName, long SizeBytes, string UploadedByUserId, DateTime CreatedUtc);

public sealed record DiscussionDetailDto(
    Guid Id, string Title, string Body, string Scope, string AuthorUserId, string AuthorDisplayName, DateTime CreatedUtc,
    bool IsPinned, bool IsLocked, bool CanModerate,
    IReadOnlyList<ReactionDto> Reactions,
    IReadOnlyList<ReplyDto> Replies,
    IReadOnlyList<AttachmentDto> Attachments);

/// <summary>A KOC user who can be @mentioned, for the autocomplete picker.</summary>
public sealed record MentionCandidateDto(string UserId, string DisplayName, string AvatarIcon);
