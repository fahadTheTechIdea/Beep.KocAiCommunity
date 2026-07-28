namespace Beep.KocAiCommunity.Application.ML;

/// <summary>
/// A supported (or roadmapped) ML task, its headline metrics, and an O&amp;G example. Forecasting runs on
/// the regression engine paired with a chronological (time-ordered) hold-out — the <c>time-split</c> node —
/// so the model trains on the past and is scored on the future. Anomaly detection is still roadmap
/// (Supported = false): it needs unsupervised learners and rare-positive metrics the engine doesn't have yet.
/// </summary>
public sealed record MlTaskDescriptor(
    string Key, MlTaskType? Task, string DisplayName, string PrimaryMetric, string SecondaryMetric, bool Supported, string OgExample);

/// <summary>The catalog of ML tasks the runtime advertises.</summary>
public static class MlTaskCatalog
{
    public static readonly IReadOnlyList<MlTaskDescriptor> All =
    [
        new("binary", MlTaskType.BinaryClassification, "Binary classification", "Accuracy", "AUC", true,
            "Predict ESP pump failure (fail / no-fail) from sensor readings."),
        new("multiclass", MlTaskType.MulticlassClassification, "Multiclass classification", "MicroAccuracy", "MacroAccuracy", true,
            "Classify well-log intervals into rock facies."),
        new("regression", MlTaskType.Regression, "Regression", "RSquared", "RMSE", true,
            "Estimate a well's production rate from reservoir + operating features."),
        new("anomaly", null, "Anomaly detection", "AUC", "DetectionRate", false,
            "Flag abnormal HSE sensor spikes (roadmap)."),
        new("forecasting", MlTaskType.Regression, "Time-series forecasting", "RMSE", "MAE", true,
            "Forecast production decline over time — add a Chronological split node to train on the past and score on the future."),
    ];

    public static MlTaskDescriptor? Find(string key) =>
        All.FirstOrDefault(t => string.Equals(t.Key, key, StringComparison.OrdinalIgnoreCase));
}
