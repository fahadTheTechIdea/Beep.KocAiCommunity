namespace Beep.KocAiCommunity.Contracts.Workflow;

public sealed record CreateWorkflowRequest(string Name, string Description, string Classification, Guid? CompetitionId = null);

public sealed record SaveDraftRequest(string DefinitionJson, string? Notes);

public sealed record ImportWorkflowRequest(string Name, string EnvelopeJson);

public sealed record InstantiateTemplateRequest(string Name);

/// <summary>A version's metadata (no graph body).</summary>
public sealed record WorkflowVersionDto(
    int VersionNumber, string Status, int SchemaVersion, string SnapshotHash, string? Notes, DateTime? PublishedUtc, DateTime CreatedUtc);

/// <summary>A version including its graph JSON.</summary>
public sealed record WorkflowVersionDetailDto(
    int VersionNumber, string Status, int SchemaVersion, string DefinitionJson, string SnapshotHash, string? Notes);

public sealed record WorkflowSummaryDto(
    Guid Id, string Name, string Description, string OwnerUserId, string Classification,
    int LatestVersionNumber, int VersionCount, string LatestStatus, DateTime CreatedUtc,
    Guid? CompetitionId = null, string? CompetitionTitle = null);

public sealed record WorkflowDetailDto(
    Guid Id, string Name, string Description, string OwnerUserId, string Classification,
    int LatestVersionNumber, IReadOnlyList<WorkflowVersionDto> Versions,
    Guid? CompetitionId = null, string? CompetitionTitle = null);

public sealed record WorkflowTemplateDto(string Code, string DisplayName, string Description, string Domain, int SchemaVersion);

/// <summary>A workflow export envelope's transport wrapper for the export endpoint.</summary>
public sealed record WorkflowExportDto(string EnvelopeJson);

/// <summary>Run a published workflow version against a dataset (enqueues a durable job).</summary>
public sealed record RunWorkflowVersionRequest(Guid DatasetId, string LabelColumn, string Task, int? MaxSeconds);

/// <summary>The id of the durable run (job) that was enqueued.</summary>
public sealed record RunEnqueuedDto(Guid RunId);
