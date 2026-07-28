namespace Beep.KocAiCommunity.Ui.Shared.Components;

/// <summary>
/// The human name for an ML task type, shared by every surface that shows one — the arena cards, the
/// competition page, and the Studio designer's competition ribbon — so a task never reads as a bare enum
/// name in one place and a friendly label in another.
/// </summary>
public static class MlTaskLabels
{
    public static string Display(string taskType) => taskType switch
    {
        "BinaryClassification" => "Classification",
        "MulticlassClassification" => "Multiclass",
        "Regression" => "Regression",
        "Forecasting" => "Forecasting",
        "AnomalyDetection" => "Anomaly detection",
        _ => taskType,
    };
}
