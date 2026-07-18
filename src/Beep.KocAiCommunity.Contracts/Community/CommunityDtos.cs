namespace Beep.KocAiCommunity.Contracts.Community;

public sealed record CreateDiscussionRequest(string Title, string Body, string Scope);

public sealed record CreateReplyRequest(string Body);

public sealed record ReplyDto(Guid Id, string AuthorUserId, string Body, DateTime CreatedUtc);

public sealed record DiscussionDto(Guid Id, string Title, string Body, string Scope, string AuthorUserId, DateTime CreatedUtc, int ReplyCount);

public sealed record DiscussionDetailDto(
    Guid Id, string Title, string Body, string Scope, string AuthorUserId, DateTime CreatedUtc, IReadOnlyList<ReplyDto> Replies);
