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
}
