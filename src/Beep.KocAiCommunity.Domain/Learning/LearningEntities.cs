using Beep.KocAiCommunity.Domain.Common;
using Beep.KocAiCommunity.Domain.Organization;

namespace Beep.KocAiCommunity.Domain.Learning;

public enum TrackLevel
{
    Beginner = 0,
    Intermediate = 1,
    Advanced = 2,
}

/// <summary>A guided learning track — the "learn" half of learn &amp; compete.</summary>
public class LearningTrack : AuditableEntity
{
    public string Title { get; set; } = default!;
    public string Summary { get; set; } = default!;
    public TrackLevel Level { get; set; }
    public int OrderNo { get; set; }
    public string Status { get; set; } = "draft";       // draft, published, archived
    public string Domain { get; set; } = "upstream";     // upstream, midstream, downstream, hse

    public VisibilityScope VisibilityScope { get; set; } = VisibilityScope.Company;
    public Guid VisibilityOrgUnitId { get; set; }        // ignored for Company scope

    public Guid? RecommendedCompetitionId { get; set; }  // learn ↔ compete tie-in
}

/// <summary>An ordered lesson within a track. Content is markdown stored via IArtifactStore.</summary>
public class Lesson : AuditableEntity
{
    public Guid TrackId { get; set; }
    public int OrderNo { get; set; }
    public string Title { get; set; } = default!;
    public string ContentRef { get; set; } = default!;   // markdown artifact reference (external content)
    public string? Content { get; set; }                 // inline markdown body (seeded/authored content)
    public int EstimatedMinutes { get; set; }
    public string? HandsOnKind { get; set; }             // null, "workflow-template", "dataset"
    public Guid? HandsOnRefId { get; set; }
}

/// <summary>A person's enrollment in a track. One per (user, track).</summary>
public class TrackEnrollment : AuditableEntity
{
    public Guid TrackId { get; set; }
    public string UserId { get; set; } = default!;
    public string Status { get; set; } = "active";       // active, completed, abandoned
    public DateTime StartedUtc { get; set; }
    public DateTime? CompletedUtc { get; set; }
}

/// <summary>Per-lesson progress within an enrollment.</summary>
public class LessonProgress : AuditableEntity
{
    public Guid EnrollmentId { get; set; }
    public Guid LessonId { get; set; }
    public string Status { get; set; } = "not-started";  // not-started, in-progress, completed
    public DateTime? CompletedUtc { get; set; }
}

/// <summary>Recorded when every lesson in a track is complete. Feeds badges + suggestions.</summary>
public class TrackCompletion : AuditableEntity
{
    public Guid TrackId { get; set; }
    public string UserId { get; set; } = default!;
    public DateTime CompletedUtc { get; set; }
}
