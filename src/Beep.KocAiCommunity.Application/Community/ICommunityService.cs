using Beep.KocAiCommunity.Domain.Community;
using Beep.KocAiCommunity.Domain.Organization;

namespace Beep.KocAiCommunity.Application.Community;

/// <summary>Raised when a discussion action is not permitted (visibility, missing thread).</summary>
public sealed class CommunityException(string message) : Exception(message);

/// <summary>A discussion with its replies.</summary>
public sealed record DiscussionThread(Discussion Discussion, IReadOnlyList<DiscussionReply> Replies);

public interface ICommunityService
{
    Task<Discussion> CreateAsync(string userId, string title, string body, VisibilityScope scope, CancellationToken ct = default);
    Task<IReadOnlyList<Discussion>> BrowseVisibleAsync(string userId, CancellationToken ct = default);
    Task<DiscussionThread?> GetVisibleAsync(string userId, Guid discussionId, CancellationToken ct = default);
    Task<DiscussionReply> AddReplyAsync(string userId, Guid discussionId, string body, CancellationToken ct = default);
}
