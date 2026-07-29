namespace Beep.KocAiCommunity.Ui.Shared.Components;

/// <summary>
/// The human name for an ML task type, shared by every surface that shows one — the arena cards, the
/// competition page, and the Studio designer's competition ribbon — so a task never reads as a bare enum
/// name in one place and a friendly label in another.
/// <para>
/// <see cref="Display"/> returns English, which callers pass through the string localizer as a key. That
/// hides these labels from the coverage test, which only sees literal <c>L["..."]</c> in markup — so
/// <see cref="Translatable"/> declares them for it. A label added to the switch and not to that array
/// simply never gets translated, and nobody finds out.
/// </para>
/// </summary>
public static class MlTaskLabels
{
    /// <summary>Every label <see cref="Display"/> can return, for the localization coverage test.</summary>
    public static readonly string[] Translatable =
        ["Classification", "Multiclass", "Regression", "Forecasting", "Anomaly detection"];

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
