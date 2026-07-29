namespace Beep.KocAiCommunity.Contracts.Competitions;

/// <summary>Create a competition. Scope is Team/Group/Directorate/Company.</summary>
public sealed record CreateCompetitionRequest(
    string Title,
    string Description,
    string Scope,
    Guid? VisibilityOrgUnitId,
    DateTime? RevealUtc,
    int QuotaPerDay,
    string ScorerCode);

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
    string? CategoryName = null);

/// <summary>A competition category — a KOC operational domain, managed by the platform admin.</summary>
public sealed record CompetitionCategoryDto(
    string Code, string Name, string Description, string Icon, bool IsEnabled, int OrderNo, int CompetitionCount);

/// <summary>Creates or updates a category, keyed by <paramref name="Code"/>.</summary>
public sealed record UpsertCompetitionCategoryRequest(
    string Code, string Name, string Description = "", string Icon = "", bool IsEnabled = true, int OrderNo = 0);

/// <summary>Assigns a competition to a category, or clears it when null.</summary>
public sealed record SetCompetitionCategoryRequest(string? Code);

/// <summary>Set (or clear, with null) the web-relative path of a competition's hero image.</summary>
public sealed record SetHeroImagePathRequest(string? Path);

public sealed record SetStatusRequest(string Status);

/// <summary>Admin-set podium prizes (free text; blank clears).</summary>
public sealed record SetPrizesRequest(string? FirstPrize, string? SecondPrize, string? ThirdPrize);

public sealed record SetRevealRequest(DateTime? RevealUtc);

public sealed record SubmissionResultDto(Guid SubmissionId, double? Score, string Status, DateTime SubmittedUtc);

public sealed record LeaderboardEntryDto(int Rank, string UserId, string DisplayName, double Score);
