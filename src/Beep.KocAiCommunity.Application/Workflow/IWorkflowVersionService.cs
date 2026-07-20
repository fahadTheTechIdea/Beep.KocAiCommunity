using Beep.KocAiCommunity.Contracts.Workflow;
using Beep.KocAiCommunity.Domain.Common;
using Beep.KocAiCommunity.Domain.Studio;
using WorkflowEntity = Beep.KocAiCommunity.Domain.Studio.Workflow;

namespace Beep.KocAiCommunity.Application.Workflow;

/// <summary>Raised when a workflow registry action is not permitted (authz, immutability, not found).</summary>
public sealed class WorkflowRegistryException(string message) : Exception(message);

/// <summary>A workflow with its version count and the status of its latest version, for listing.</summary>
public sealed record WorkflowSummary(WorkflowEntity Workflow, int VersionCount, string LatestStatus);

/// <summary>A workflow with all of its versions, newest first.</summary>
public sealed record WorkflowDetail(WorkflowEntity Workflow, IReadOnlyList<WorkflowVersion> Versions);

/// <summary>
/// Manages workflows and their immutable versions: create, edit a draft, publish (validated and then
/// frozen), archive, export, and import. Publishing runs the compiler; published versions can't change.
/// </summary>
public interface IWorkflowVersionService
{
    Task<WorkflowEntity> CreateAsync(string userId, string name, string description, KocDataClassification classification, Guid? competitionId = null, CancellationToken ct = default);
    Task<IReadOnlyList<WorkflowSummary>> ListAsync(string userId, CancellationToken ct = default);
    Task<WorkflowDetail?> GetAsync(string userId, Guid workflowId, CancellationToken ct = default);
    Task<WorkflowVersion?> GetVersionAsync(string userId, Guid workflowId, int versionNumber, CancellationToken ct = default);

    /// <summary>Saves the graph to the latest draft, or opens a new draft if the latest is published.</summary>
    Task<WorkflowVersion> SaveDraftAsync(string userId, bool isAdmin, Guid workflowId, string definitionJson, string? notes, CancellationToken ct = default);

    /// <summary>Validates then publishes a draft, freezing it. Fails if the graph doesn't compile.</summary>
    Task<WorkflowVersion> PublishAsync(string userId, bool isAdmin, Guid workflowId, int versionNumber, CancellationToken ct = default);
    Task<WorkflowVersion> ArchiveAsync(string userId, bool isAdmin, Guid workflowId, int versionNumber, CancellationToken ct = default);
    Task DeleteAsync(string userId, bool isAdmin, Guid workflowId, CancellationToken ct = default);

    /// <summary>Compiles a version's graph (cycle/kind/required-node checks) without changing it.</summary>
    Task<WorkflowValidationResult> ValidateAsync(string userId, Guid workflowId, int versionNumber, CancellationToken ct = default);

    /// <summary>A portable JSON envelope of a version (schema + canonical definition + hash).</summary>
    Task<string> ExportAsync(string userId, Guid workflowId, int versionNumber, CancellationToken ct = default);

    /// <summary>Creates a new workflow (draft v1) from an exported envelope.</summary>
    Task<WorkflowEntity> ImportAsync(string userId, string name, string envelopeJson, CancellationToken ct = default);
}

/// <summary>Seeded, read-only workflow starting points a user can instantiate.</summary>
public interface IWorkflowTemplateService
{
    Task<IReadOnlyList<WorkflowTemplate>> ListAsync(string? domain, CancellationToken ct = default);

    /// <summary>Creates a new workflow (draft v1) from a template's definition.</summary>
    Task<WorkflowEntity> InstantiateAsync(string userId, string code, string name, CancellationToken ct = default);
}
