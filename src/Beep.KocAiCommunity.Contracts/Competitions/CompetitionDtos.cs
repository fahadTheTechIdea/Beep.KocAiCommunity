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
    string? HeroImagePath = null);

/// <summary>Set (or clear, with null) the web-relative path of a competition's hero image.</summary>
public sealed record SetHeroImagePathRequest(string? Path);

public sealed record SetStatusRequest(string Status);

/// <summary>Admin-set podium prizes (free text; blank clears).</summary>
public sealed record SetPrizesRequest(string? FirstPrize, string? SecondPrize, string? ThirdPrize);

public sealed record SetRevealRequest(DateTime? RevealUtc);

public sealed record SubmissionResultDto(Guid SubmissionId, double? Score, string Status, DateTime SubmittedUtc);

public sealed record LeaderboardEntryDto(int Rank, string UserId, string DisplayName, double Score);
