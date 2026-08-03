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
/// <summary>
/// The states an enrollment can be in. <see cref="AwaitingQuiz"/> is the one the quiz feature adds: the
/// reading is done and a mandatory quiz is not yet passed. It is deliberately not "active" — a progress
/// bar reading 8 of 8 next to "in progress" looks like a bug rather than like one step left.
/// </summary>
public static class TrackEnrollmentStatus
{
    public const string Active = "active";
    public const string AwaitingQuiz = "awaiting-quiz";
    public const string Completed = "completed";
    public const string Abandoned = "abandoned";
}

public class TrackEnrollment : AuditableEntity
{
    public Guid TrackId { get; set; }
    public string UserId { get; set; } = default!;
    public string Status { get; set; } = TrackEnrollmentStatus.Active;
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

// ── Quizzes ─────────────────────────────────────────────────────────────────────────────────────
// A track may end in a quiz. Whether passing it is required to finish the track is the admin's call,
// per track — so the same machinery serves a light self-check on an introductory track and a real
// assessment on one that matters.

/// <summary>
/// The quiz at the end of a track. At most one per track.
/// <para>
/// <see cref="IsMandatory"/> is the whole point of the feature: when it is set, finishing every lesson
/// is no longer finishing the track — the completion, its Barrels and its badge wait until the quiz is
/// passed. When it is not, the quiz is a self-check that records a score and blocks nothing.
/// </para>
/// </summary>
public class Quiz : AuditableEntity
{
    public Guid TrackId { get; set; }

    /// <summary>Percentage of questions that must be answered correctly, 1–100.</summary>
    public int PassMark { get; set; } = 70;

    /// <summary>Whether passing gates completion of the track.</summary>
    public bool IsMandatory { get; set; }

    /// <summary>
    /// Off hides the quiz from learners entirely. Separate from deleting it, so a quiz can be drafted,
    /// or withdrawn after a bad question is spotted, without losing the questions or anyone's attempts.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>Shown above the questions — what this covers, how long it takes.</summary>
    public string Intro { get; set; } = string.Empty;
}

public class QuizQuestion : AuditableEntity
{
    public Guid QuizId { get; set; }
    public int OrderNo { get; set; }
    public string Text { get; set; } = default!;

    /// <summary>
    /// Why the right answer is right, shown in the review after an attempt. A quiz that only says
    /// "wrong" teaches nothing, which for a learning platform is the point missed.
    /// </summary>
    public string Explanation { get; set; } = string.Empty;
}

/// <summary>
/// One option on a question. A question may have more than one correct answer; a response counts only
/// when it selects every correct option and none of the incorrect ones.
/// </summary>
public class QuizAnswer : AuditableEntity
{
    public Guid QuestionId { get; set; }
    public int OrderNo { get; set; }
    public string Text { get; set; } = default!;
    public bool IsCorrect { get; set; }
}

/// <summary>
/// One sitting of a quiz. Kept whether it passed or failed: the attempt count is what "passed first
/// time" is measured against, and deleting failures would quietly make everybody perfect.
/// </summary>
public class QuizAttempt : AuditableEntity
{
    public Guid QuizId { get; set; }
    public string UserId { get; set; } = default!;

    /// <summary>1 for this person's first sitting of this quiz, counting up.</summary>
    public int AttemptNo { get; set; }

    public DateTime SubmittedUtc { get; set; }
    public int CorrectCount { get; set; }
    public int QuestionCount { get; set; }

    /// <summary>Rounded percentage, stored rather than derived so a later edit to the quiz cannot silently restate an old result.</summary>
    public int ScorePercent { get; set; }

    /// <summary>Judged against the pass mark as it stood when the attempt was taken.</summary>
    public bool Passed { get; set; }
}

/// <summary>What was actually selected, so an attempt can be reviewed question by question.</summary>
public class QuizAttemptAnswer : AuditableEntity
{
    public Guid AttemptId { get; set; }
    public Guid QuestionId { get; set; }
    public Guid AnswerId { get; set; }
}
