using Beep.KocAiCommunity.Domain.Studio;

namespace Beep.KocAiCommunity.Application.Studio;

/// <summary>Raised when a registry action is not permitted (missing run, insufficient approvals).</summary>
public sealed class ModelRegistryException(string message) : Exception(message);

/// <summary>A model with its versions and approval counts, for listing.</summary>
public sealed record ModelSummary(RegisteredModel Model, IReadOnlyList<ModelVersion> Versions, IReadOnlyDictionary<Guid, int> ApprovalCounts);

/// <summary>An active/retired deployment joined to its model name and version, for listing.</summary>
public sealed record DeploymentSummary(ModelDeployment Deployment, string ModelName, string SemVer, string MetricName, double MetricValue);

/// <summary>
/// The model registry: register a training run as an immutable version, gather approvals, and
/// promote to production only with two distinct approvals. Requires <c>RequiredApprovals</c> = 2.
/// </summary>
public interface IModelRegistry
{
    int RequiredApprovals => 2;

    Task<ModelVersion> RegisterAsync(string userId, string modelName, Guid sourceRunId, CancellationToken ct = default);
    Task<IReadOnlyList<ModelSummary>> ListAsync(CancellationToken ct = default);
    Task<ModelApproval> ApproveAsync(string userId, Guid versionId, CancellationToken ct = default);
    Task<ModelVersion> PromoteAsync(string userId, Guid versionId, CancellationToken ct = default);
    Task<ModelVersion> RollbackAsync(string userId, Guid versionId, CancellationToken ct = default);

    /// <summary>Deploys a production version to an environment, retiring the prior active one there.</summary>
    Task<ModelDeployment> DeployAsync(string userId, Guid versionId, string environment, CancellationToken ct = default);
    Task RetireAsync(string userId, Guid deploymentId, CancellationToken ct = default);
    Task<IReadOnlyList<DeploymentSummary>> ListDeploymentsAsync(CancellationToken ct = default);
}
