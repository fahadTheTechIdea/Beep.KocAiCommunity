using Beep.KocAiCommunity.Domain.Common;

namespace Beep.KocAiCommunity.Domain.Studio;

/// <summary>
/// A named, owned ML workflow. Its graph lives in immutable <see cref="WorkflowVersion"/> rows;
/// the workflow row itself is the stable identity that versions hang off.
/// </summary>
public class Workflow : AuditableEntity
{
    public string Name { get; set; } = default!;
    public string Description { get; set; } = default!;
    public string OwnerUserId { get; set; } = default!;
    public Guid? ProjectId { get; set; }
    public KocDataClassification Classification { get; set; } = KocDataClassification.Internal;

    /// <summary>The highest version number issued so far (draft or published).</summary>
    public int LatestVersionNumber { get; set; }
}

/// <summary>
/// One version of a workflow's graph. Drafts are editable; once published (or archived) the
/// <see cref="DefinitionJson"/> and <see cref="SnapshotHash"/> are immutable.
/// </summary>
public class WorkflowVersion : AuditableEntity
{
    public Guid WorkflowId { get; set; }
    public int VersionNumber { get; set; }
    public string Status { get; set; } = "draft";   // draft, published, archived
    public int SchemaVersion { get; set; } = 1;
    public string DefinitionJson { get; set; } = default!;
    public string SnapshotHash { get; set; } = default!;   // SHA-256 of DefinitionJson
    public string? Notes { get; set; }
    public DateTime? PublishedUtc { get; set; }
    public string? PublishedByUserId { get; set; }
}

/// <summary>A seeded, read-only starting point a user can instantiate into their own workflow.</summary>
public class WorkflowTemplate : AuditableEntity
{
    public string Code { get; set; } = default!;
    public string DisplayName { get; set; } = default!;
    public string Description { get; set; } = default!;
    public string Domain { get; set; } = default!;
    public string DefinitionJson { get; set; } = default!;
    public int SchemaVersion { get; set; } = 1;
    public string SnapshotHash { get; set; } = default!;
}
