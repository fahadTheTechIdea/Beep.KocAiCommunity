namespace Beep.KocAiCommunity.Contracts.Workflow;

public sealed record CreateWorkflowRequest(string Name, string Description, string Classification);

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
    int LatestVersionNumber, int VersionCount, string LatestStatus, DateTime CreatedUtc);

public sealed record WorkflowDetailDto(
    Guid Id, string Name, string Description, string OwnerUserId, string Classification,
    int LatestVersionNumber, IReadOnlyList<WorkflowVersionDto> Versions);

public sealed record WorkflowTemplateDto(string Code, string DisplayName, string Description, string Domain, int SchemaVersion);

/// <summary>A workflow export envelope's transport wrapper for the export endpoint.</summary>
public sealed record WorkflowExportDto(string EnvelopeJson);
