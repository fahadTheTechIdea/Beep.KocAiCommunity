namespace Beep.KocAiCommunity.Contracts.Competitions;

/// <summary>Create a competition. Scope is Team/Group/Directorate/Company.</summary>
public sealed record CreateCompetitionRequest(
    string Title,
    string Description,
    string Scope,
    Guid? VisibilityOrgUnitId,
    DateTime? RevealUtc,
    int QuotaPerDay,
    string ScorerCode,
    // Optional Arabic. Left null the challenge simply reads in English for an Arabic visitor, which is
    // the same bargain the interface strings make — a gap shows English, never a blank.
    string? TitleAr = null,
    string? DescriptionAr = null,
    // The task half of the task-and-metric pair. Appended and optional so older callers keep working:
    // left null the server falls back to the scorer's first supported task, which is what the old
    // behavior accidentally was whenever the two happened to agree.
    string? TaskType = null);

/// <summary>What a competition says in one non-English language, for the author to fill in or edit.</summary>
public sealed record CompetitionTranslationDto(string Language, string? Title, string? Description);

/// <summary>Replaces the translation for one language. Blank fields clear it back to the original.</summary>
public sealed record SetCompetitionTranslationRequest(string Language, string? Title, string? Description);

public sealed record CompetitionDto(
    Guid Id,
    string Title,
    string Description,
    string Status,
    string Scope,
    DateTime? RevealUtc,
    bool HasAnswerKey,
    bool HasDatasets,
    string LabelColumn,
    string IdColumn,
    string TaskType,
    Guid? RecommendedTrackId,
    // Arena enrichment (appended, optional — older callers keep working).
    int ParticipantCount = 0,
    int SubmissionCount = 0,
    string HostName = "",
    int QuotaPerDay = 0,
    string MetricName = "",
    bool HigherIsBetter = true,
    DateTime? CreatedUtc = null,
    bool IsFeatured = false,
    string? FirstPrize = null,
    string? SecondPrize = null,
    string? ThirdPrize = null,
    bool HasHeroImage = false,
    // Web-relative path of the hero image (served from the web app's wwwroot), or null.
    string? HeroImagePath = null,
    /// <summary>The category's code and display name, or null when the competition is uncategorised.</summary>
    string? CategoryCode = null,
    string? CategoryName = null,
    /// <summary>The scoring identity: the scorer's code and the tasks it can score. What the Host
    /// console uses to keep the Task select inside the metric's family.</summary>
    string? ScorerCode = null,
    IReadOnlyList<string>? SupportedTasks = null,
    /// <summary>True when the caller may run this competition's console — its creator or a platform
    /// admin. Computed server-side; the client never guesses.</summary>
    bool CanManage = false,
    /// <summary>How many submissions the caller has left today, on the single-competition read for a
    /// signed-in caller; null on lists and for visitors.</summary>
    int? MyQuotaRemainingToday = null,
    /// <summary>When true, the competition concludes itself at the reveal moment.</summary>
    bool ConcludeAtReveal = false);

/// <summary>A competition category — a KOC operational domain, managed by the platform admin.</summary>
public sealed record CompetitionCategoryDto(
    string Code, string Name, string Description, string Icon, bool IsEnabled, int OrderNo, int CompetitionCount);

/// <summary>Creates or updates a category, keyed by <paramref name="Code"/>.</summary>
public sealed record UpsertCompetitionCategoryRequest(
    string Code, string Name, string Description = "", string Icon = "", bool IsEnabled = true, int OrderNo = 0);

/// <summary>Assigns a competition to a category, or clears it when null.</summary>
public sealed record SetCompetitionCategoryRequest(string? Code);

/// <summary>
/// The Host console's edit of the competition's own words and rules. Scope is applied only while the
/// competition is a draft — widening or narrowing a live audience would silently change who can see
/// submissions already made — and is ignored (null or unchanged) otherwise.
/// </summary>
public sealed record UpdateCompetitionRequest(string Title, string Description, int QuotaPerDay, string? Scope = null);

/// <summary>Set (or clear, with null) the web-relative path of a competition's hero image.</summary>
public sealed record SetHeroImagePathRequest(string? Path);

public sealed record SetStatusRequest(string Status);

/// <summary>Admin-set podium prizes (free text; blank clears).</summary>
public sealed record SetPrizesRequest(string? FirstPrize, string? SecondPrize, string? ThirdPrize);

/// <summary>Sets or clears the reveal moment; optionally whether the competition concludes itself then.</summary>
public sealed record SetRevealRequest(DateTime? RevealUtc, bool? ConcludeAtReveal = null);

public sealed record SubmissionResultDto(Guid SubmissionId, double? Score, string Status, DateTime SubmittedUtc);

public sealed record LeaderboardEntryDto(int Rank, string UserId, string DisplayName, double Score);
