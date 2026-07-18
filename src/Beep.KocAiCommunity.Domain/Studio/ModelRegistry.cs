using Beep.KocAiCommunity.Domain.Common;

namespace Beep.KocAiCommunity.Domain.Studio;

/// <summary>A named model in the registry; holds one or more versions.</summary>
public class RegisteredModel : AuditableEntity
{
    public string Name { get; set; } = default!;
    public string OwnerUserId { get; set; } = default!;
    public string? Description { get; set; }
}

/// <summary>
/// An immutable version of a model, produced from a training run. Promoted to production only
/// after two distinct approvals; the previous production version is archived on promotion.
/// </summary>
public class ModelVersion : AuditableEntity
{
    public Guid ModelId { get; set; }
    public string SemVer { get; set; } = default!;
    public string Status { get; set; } = "staging";   // staging, production, archived
    public Guid SourceRunId { get; set; }
    public string MetricName { get; set; } = default!;
    public double MetricValue { get; set; }
    public string RegisteredByUserId { get; set; } = default!;
}

/// <summary>An approval decision on a model version.</summary>
public class ModelApproval : AuditableEntity
{
    public Guid ModelVersionId { get; set; }
    public string ApproverUserId { get; set; } = default!;
    public string Decision { get; set; } = "approve";  // approve, reject
    public string? Notes { get; set; }
}

/// <summary>
/// A deployment of a production model version to an environment. Only one deployment is active per
/// (model, environment) at a time; deploying a new version retires the previous one.
/// </summary>
public class ModelDeployment : AuditableEntity
{
    public Guid ModelVersionId { get; set; }
    public string Environment { get; set; } = "production";  // staging, production
    public string Status { get; set; } = "active";           // active, retired
    public string DeployedByUserId { get; set; } = default!;
    public DateTime DeployedUtc { get; set; }
    public DateTime? RetiredUtc { get; set; }
}
