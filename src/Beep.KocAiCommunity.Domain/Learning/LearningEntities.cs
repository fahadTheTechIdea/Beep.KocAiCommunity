using Beep.KocAiCommunity.Domain.Common;
using Beep.KocAiCommunity.Domain.Organization;

namespace Beep.KocAiCommunity.Domain.Learning;

/// <summary>
/// The languages learning content is published in. KOC's workforce reads both, and the material is the
/// half of the platform open to everyone — so it is the half that most needs to be readable in Arabic.
/// </summary>
public static class TrackLanguages
{
    public const string English = "en";
    public const string Arabic = "ar";

    /// <summary>Every supported language, in the order a picker should offer them.</summary>
    public static readonly string[] All = [English, Arabic];

    /// <summary>The name of a language in that language, for a picker that reads naturally.</summary>
    public static string NativeName(string language) => language switch
    {
        Arabic => "العربية",
        _ => "English",
    };

    /// <summary>Arabic is read right to left; the pages that render a track need to know.</summary>
    public static bool IsRightToLeft(string language) =>
        string.Equals(language, Arabic, StringComparison.OrdinalIgnoreCase);

    /// <summary>Falls back to English for anything unrecognised, so a bad query string reads normally.</summary>
    public static string Normalize(string? language) =>
        All.FirstOrDefault(l => string.Equals(l, language, StringComparison.OrdinalIgnoreCase)) ?? English;
}

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

    /// <summary>
    /// The language this track is written in, as an ISO 639-1 code — <c>en</c> or <c>ar</c> today.
    /// <para>
    /// A translation is a separate track, not a column on this one: its lessons differ in number and
    /// length, and a reader progresses through the version they can read. Two rows keep both honest.
    /// </para>
    /// </summary>
    public string Language { get; set; } = TrackLanguages.English;

    /// <summary>
    /// Identifies the same material across languages — <c>anomaly-detection</c> names both the English
    /// track and its Arabic translation. Empty for tracks an author never paired.
    /// </summary>
    public string ContentKey { get; set; } = string.Empty;
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
