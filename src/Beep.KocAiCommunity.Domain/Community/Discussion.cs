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

    /// <summary>Locked threads accept no new replies (set by a moderator).</summary>
    public bool IsLocked { get; set; }

    /// <summary>Pinned threads sort to the top of the list (set by a moderator).</summary>
    public bool IsPinned { get; set; }
}

/// <summary>A reply on a discussion.</summary>
public class DiscussionReply : AuditableEntity
{
    public Guid DiscussionId { get; set; }
    public string AuthorUserId { get; set; } = default!;
    public string Body { get; set; } = default!;
}

/// <summary>
/// An emoji reaction on a discussion or reply. One row per (target, user, emoji); reacting again with
/// the same emoji toggles it off. <see cref="TargetType"/> is "discussion" or "reply".
/// </summary>
public class Reaction : AuditableEntity
{
    public string TargetType { get; set; } = default!;
    public Guid TargetId { get; set; }
    public string UserId { get; set; } = default!;
    public string Emoji { get; set; } = default!;
}

/// <summary>
/// A resolved @mention of a KOC user inside a discussion or reply. <see cref="SourceType"/> is
/// "discussion" or "reply". Only existing KOC profiles can be mentioned.
/// </summary>
public class Mention : AuditableEntity
{
    public string SourceType { get; set; } = default!;
    public Guid SourceId { get; set; }
    public string MentionedUserId { get; set; } = default!;
    public string ByUserId { get; set; } = default!;
}

/// <summary>A file attached to a discussion, stored via the artifact service and classified.</summary>
public class DiscussionAttachment : AuditableEntity
{
    public Guid DiscussionId { get; set; }
    public Guid ArtifactReferenceId { get; set; }
    public string FileName { get; set; } = default!;
    public long SizeBytes { get; set; }
    public string UploadedByUserId { get; set; } = default!;
}
