using Beep.KocAiCommunity.Domain.Common;

namespace Beep.KocAiCommunity.Domain.Studio;

/// <summary>
/// A recorded ML.NET AutoML training run. Metrics are task-appropriate: Accuracy/AUC for binary,
/// MicroAccuracy/MacroAccuracy for multiclass, R²/RMSE for regression.
/// </summary>
public class ModelRun : AuditableEntity
{
    public string DatasetName { get; set; } = default!;
    public string LabelColumn { get; set; } = default!;
    public string Task { get; set; } = "BinaryClassification";
    public string Algorithm { get; set; } = default!;
    public string PrimaryMetric { get; set; } = default!;
    public double PrimaryValue { get; set; }
    public string SecondaryMetric { get; set; } = default!;
    public double SecondaryValue { get; set; }
    public long RowCount { get; set; }
    public string RunByUserId { get; set; } = default!;
    public DateTime CompletedUtc { get; set; }

    /// <summary>
    /// Artifact reference of the saved ML.NET model (.zip). Null for runs recorded before model
    /// persistence was added, or when training produced no serializable model. Required for inference.
    /// </summary>
    public Guid? ModelArtifactId { get; set; }

    /// <summary>
    /// Per-feature baseline statistics (count/mean/min/max of numeric columns) captured from the
    /// training data. Serves as the drift baseline compared against future inference batches.
    /// </summary>
    public string? FeatureStatsJson { get; set; }
}
