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
    DateTime? CreatedUtc = null);

public sealed record SetStatusRequest(string Status);

public sealed record SetRevealRequest(DateTime? RevealUtc);

public sealed record SubmissionResultDto(Guid SubmissionId, double? Score, string Status, DateTime SubmittedUtc);

public sealed record LeaderboardEntryDto(int Rank, string UserId, string DisplayName, double Score);
