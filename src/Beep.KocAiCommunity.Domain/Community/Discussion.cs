using Beep.KocAiCommunity.Domain.Common;
using Beep.KocAiCommunity.Domain.Organization;

namespace Beep.KocAiCommunity.Domain.Community;

/// <summary>A discussion thread, visible within an org scope. Internal to KOC.</summary>
public class Discussion : AuditableEntity
{
    public string Title { get; set; } = default!;
    public string Body { get; set; } = default!;
    public string AuthorUserId { get; set; } = default!;

    public VisibilityScope VisibilityScope { get; set; } = VisibilityScope.Team;
    public Guid VisibilityOrgUnitId { get; set; }
}

/// <summary>A reply on a discussion.</summary>
public class DiscussionReply : AuditableEntity
{
    public Guid DiscussionId { get; set; }
    public string AuthorUserId { get; set; } = default!;
    public string Body { get; set; } = default!;
}
