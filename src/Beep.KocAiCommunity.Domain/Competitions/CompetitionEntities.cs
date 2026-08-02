using Beep.KocAiCommunity.Domain.Common;
using Beep.KocAiCommunity.Domain.Organization;

namespace Beep.KocAiCommunity.Domain.Competitions;

/// <summary>
/// An internal, Kaggle-style KOC competition. Scored on a hidden answer key; visible only within
/// its org-scoped audience. The concealed final leaderboard is revealed at <see cref="RevealUtc"/>.
/// </summary>
public class Competition : AuditableEntity
{
    public string Title { get; set; } = default!;
    public string Description { get; set; } = default!;
    public string Status { get; set; } = "active";      // draft, active, concluded

    public VisibilityScope VisibilityScope { get; set; } = VisibilityScope.Team;
    public Guid VisibilityOrgUnitId { get; set; }

    public DateTime? RevealUtc { get; set; }
    public int SubmissionQuotaPerDay { get; set; } = 5;

    public string ScorerCode { get; set; } = "accuracy";  // trusted server-side scorer
    public Guid? TrainingDatasetArtifactId { get; set; }   // labelled training data (visible to participants)
    public Guid? EvaluationArtifactId { get; set; }        // id + features, no label (visible to participants)
    public Guid? AnswerKeyArtifactId { get; set; }         // hidden true labels for the evaluation set
    public string LabelColumn { get; set; } = "label";     // column names the pipeline runner needs
    public string IdColumn { get; set; } = "id";
    public string TaskType { get; set; } = "BinaryClassification";
    public Guid? RecommendedTrackId { get; set; }          // learn ↔ compete tie-in
    public bool IsFeatured { get; set; }                   // pinned as THE landing-page hero (one at a time; admin-set)

    // Admin-set podium prizes (free text, e.g. "1,000 Barrels + Gusher trophy"). Optional; when unset the
    // arena falls back to the standard barrel rewards.
    public string? FirstPrize { get; set; }
    public string? SecondPrize { get; set; }
    public string? ThirdPrize { get; set; }

    // Optional hero image shown as the competition's banner background. The image file is served as a
    // static asset from the web app's wwwroot; this is just its web-relative path (e.g. "/uploads/competitions/{id}.png").
    public string? HeroImagePath { get; set; }

    /// <summary>
    /// The <see cref="CompetitionCategory.Code"/> this challenge belongs to, or null for uncategorised.
    /// Nullable on purpose: competitions created before categories existed keep working, and only an
    /// explicitly disabled category hides anything.
    /// </summary>
    public string? CategoryCode { get; set; }
}

/// <summary>
/// A grouping of competitions by KOC operational domain — Subsurface, Production, HSE and so on. The
/// list is data rather than an enum, so the platform admin shapes the catalogue without a release.
/// <para>
/// Disabling a category hides every competition in it from browsing, from the landing page, and from
/// direct links. Nothing is deleted: re-enabling restores the challenges and their leaderboards
/// exactly as they were, which is what makes it safe to use for staging a season or retiring a theme.
/// </para>
/// </summary>
public class CompetitionCategory : AuditableEntity
{
    /// <summary>Stable slug referenced by <see cref="Competition.CategoryCode"/>. Unique.</summary>
    public string Code { get; set; } = default!;

    public string Name { get; set; } = default!;
    public string Description { get; set; } = string.Empty;

    /// <summary>A MudBlazor icon name shown on the filter chip and the competition card.</summary>
    public string Icon { get; set; } = string.Empty;

    /// <summary>When false the category and its competitions are hidden from everyone.</summary>
    public bool IsEnabled { get; set; } = true;

    public int OrderNo { get; set; }
}

/// <summary>One scored attempt by a participant.</summary>
public class Submission : AuditableEntity
{
    public Guid CompetitionId { get; set; }
    public string SubmitterUserId { get; set; } = default!;
    public Guid PredictionArtifactId { get; set; }
    public DateTime SubmittedUtc { get; set; }
    public string Status { get; set; } = "scored";       // pending, scored, failed
    public double? Score { get; set; }                   // public score — the live leaderboard (Kaggle-style)
    public double? PrivateScore { get; set; }            // private/hidden holdout score — the final standings
    public string? Notes { get; set; }

    /// <summary>
    /// A caller-supplied key that makes a retry safe.
    /// <para>
    /// Submissions are quota-limited, so a retried request that scores twice costs the participant an
    /// attempt they did not spend. A client that queues submissions — the desktop when it is offline —
    /// cannot know whether a request that timed out was received, so without this it must choose
    /// between losing work and double-spending someone's quota.
    /// </para>
    /// <para>
    /// Unique per (competition, submitter) when present. Null for the ordinary online path, where the
    /// caller is a person watching a button.
    /// </para>
    /// </summary>
    public string? IdempotencyKey { get; set; }
}

/// <summary>
/// A participant's best score and current rank on a competition's leaderboard. <see cref="Score"/>/
/// <see cref="Rank"/> are the public (live) standings — the best public-scoring submission. The private
/// score of that same submission is carried in <see cref="PrivateScore"/> for the concealed final board.
/// </summary>
public class LeaderboardEntry : AuditableEntity
{
    public Guid CompetitionId { get; set; }
    public string SubmitterUserId { get; set; } = default!;
    public Guid? BestSubmissionId { get; set; }
    public double Score { get; set; }
    public double PrivateScore { get; set; }
    public int Rank { get; set; }
}
