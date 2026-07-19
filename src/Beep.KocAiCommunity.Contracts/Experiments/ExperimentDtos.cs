namespace Beep.KocAiCommunity.Contracts.Experiments;

public sealed record CreateExperimentRequest(string Name, string Description, Guid? ProjectId, string? Tags);

public sealed record ExperimentDto(
    Guid Id, string Name, string Description, string OwnerUserId, Guid? ProjectId, string Status,
    Guid? BestRunId, string? Tags, int RunCount, DateTime CreatedUtc);

public sealed record RunDto(
    Guid Id,
    Guid ExperimentId,
    Guid? ParentRunId,
    string RunByUserId,
    string Status,
    string Task,
    string? Algorithm,
    string? PrimaryMetric,
    double? PrimaryValue,
    string? SecondaryMetric,
    double? SecondaryValue,
    long RowCount,
    int TrialCount,
    bool IsFavorite,
    bool IsBest,
    string? Tags,
    DateTime? StartedUtc,
    DateTime? CompletedUtc,
    DateTime CreatedUtc);

public sealed record RunMetricDto(string Name, double Value, string? Dataset, string? Phase, int Step, DateTime LoggedUtc);

public sealed record RunParameterDto(string Name, string ValueJson);

public sealed record UpdateRunRequest(bool? IsFavorite, bool? IsBest, string? Tags);

public sealed record LogMetricsRequest(IReadOnlyList<RunMetricInput> Metrics);

public sealed record RunMetricInput(string Name, double Value, string? Dataset, string? Phase, int Step);

/// <summary>A run's headline metric across a comparison set (one column per run).</summary>
public sealed record ComparisonRowDto(Guid RunId, string? Algorithm, string Status, string? PrimaryMetric, double? PrimaryValue, double? SecondaryValue, bool IsBest);
