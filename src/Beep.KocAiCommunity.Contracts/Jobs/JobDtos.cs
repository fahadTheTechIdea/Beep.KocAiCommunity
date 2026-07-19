namespace Beep.KocAiCommunity.Contracts.Jobs;

/// <summary>A background run (job) summary.</summary>
public sealed record RunDto(
    Guid Id,
    string Type,
    string Title,
    string Status,
    int AttemptCount,
    int MaxAttempts,
    int Priority,
    DateTime CreatedUtc,
    DateTime? StartedUtc,
    DateTime? CompletedUtc,
    string? LastError);

public sealed record RunLogDto(DateTime LoggedUtc, string Severity, string Message);

public sealed record RunAttemptDto(int AttemptNumber, string Status, DateTime StartedUtc, DateTime? CompletedUtc, string? WorkerId, string? Error);

/// <summary>Request to enqueue a run. Payload shape depends on <paramref name="Type"/>.</summary>
public sealed record CreateRunRequest(string Type, string Title, string PayloadJson, int Priority = 0);

/// <summary>Payload for a <c>model.train</c> run.</summary>
public sealed record ModelTrainPayload(Guid DatasetId, string LabelColumn, string TaskType, int MaxSeconds, string OwnerUserId);

/// <summary>
/// Payload for an <c>experiment.train</c> run: trains with live trial tracking into an experiment
/// (created from <paramref name="ExperimentName"/> when <paramref name="ExperimentId"/> is null).
/// </summary>
public sealed record ExperimentTrainPayload(
    Guid? ExperimentId, string ExperimentName, Guid DatasetId, string LabelColumn, string TaskType, int MaxSeconds, string OwnerUserId);

/// <summary>
/// Payload for a <c>workflow.run</c> run: trains a published workflow version against a dataset with
/// live trial tracking (into a new experiment) and records a registerable <c>ModelRun</c>.
/// </summary>
public sealed record WorkflowRunPayload(
    Guid WorkflowId, int VersionNumber, string WorkflowName, Guid DatasetId, string LabelColumn, string TaskType, int MaxSeconds, string OwnerUserId);
