using MudBlazor;

namespace Beep.KocAiCommunity.Web.Components.Shared;

/// <summary>
/// One task-and-metric choice: what kind of value is predicted, and how it is scored. The two are one
/// record on purpose — they are two halves of one decision, and every place that ever let them travel
/// separately produced a competition whose task chip contradicted its metric chip.
/// </summary>
/// <param name="Key">The <c>TaskType</c> stored on the competition.</param>
/// <param name="Label">Human name, localizer key.</param>
/// <param name="Icon">Material icon for the picker card.</param>
/// <param name="Metric">Short metric label for chips, localizer key.</param>
/// <param name="Scorer">The trusted scorer's code.</param>
/// <param name="MetricPlain">The metric in plain words — what moves it, which direction wins.</param>
/// <param name="Help">When to pick this task, in the host's terms.</param>
/// <param name="SubmissionHint">What a competitor's CSV must contain, in the competitor's terms.</param>
/// <param name="TrainingSample">Three-line CSV sketch of training.csv.</param>
/// <param name="EvalSample">Three-line CSV sketch of evaluation.csv.</param>
/// <param name="KeySample">Three-line CSV sketch of the hidden answer key.</param>
/// <param name="SampleValue">The placeholder written into a generated sample_submission.csv.</param>
public sealed record CompetitionTaskOption(
    string Key, string Label, string Icon, string Metric, string Scorer, string MetricPlain, string Help,
    string SubmissionHint, string TrainingSample, string EvalSample, string KeySample, string SampleValue);

/// <summary>
/// The tasks a KOC competition can pose, shared by the launcher (choosing one) and the Host console
/// (staying inside the chosen metric's family). Grown from the create dialog's private list — two
/// surfaces reading one catalog is the point; the drift this phase removes began as two copies.
/// </summary>
public static class CompetitionTaskCatalog
{
    public static readonly IReadOnlyList<CompetitionTaskOption> All =
    [
        new("BinaryClassification", "Binary classification", Icons.Material.Filled.ToggleOn,
            "Accuracy", "accuracy",
            "Accuracy — the share of rows predicted right; higher wins.",
            "Yes/no outcomes — fails / doesn't fail, needs intervention or not.",
            "One row per evaluation id, predicting the label's own values (e.g. true/false or 1/0).",
            "id,pressure,vibration,label\nW-014,3200,4.1,1\nW-021,2750,1.2,0",
            "id,pressure,vibration\nE-001,3100,3.8\nE-002,2680,1.0",
            "id,label\nE-001,1\nE-002,0",
            "0"),
        new("MulticlassClassification", "Multiclass", Icons.Material.Filled.Category,
            "Accuracy", "accuracy",
            "Accuracy — the share of rows predicted right; higher wins.",
            "One of several classes — rock facies, failure modes, severity bands.",
            "One row per evaluation id, predicting the class name exactly as it appears in training.",
            "id,gr,resistivity,label\nD-11,82,14,sandstone\nD-12,120,4,shale",
            "id,gr,resistivity\nE-01,90,11\nE-02,118,5",
            "id,label\nE-01,sandstone\nE-02,shale",
            "unknown"),
        new("Regression", "Regression", Icons.Material.Filled.ShowChart,
            "RMSE (lower wins)", "rmse",
            "RMSE — the average size of your miss; lower wins.",
            "A number — production rate, yield, time-to-failure.",
            "One row per evaluation id, predicting the number itself.",
            "id,choke,tubing_pressure,oil_rate\nP-07,32,180,1240\nP-08,24,150,900",
            "id,choke,tubing_pressure\nE-1,30,175\nE-2,26,158",
            "id,oil_rate\nE-1,1180\nE-2,980",
            "0"),
        new("Forecasting", "Time-series forecasting", Icons.Material.Filled.Timeline,
            "RMSE (lower wins)", "rmse",
            "RMSE — the average size of your miss; lower wins.",
            "A number over time — production decline, demand. Competitors train on the past and are scored on the future.",
            "One row per evaluation id, predicting the number for that future point.",
            "id,date,days_online,oil_rate\nW7-01,2024-01-01,10,1240\nW7-02,2024-01-02,11,1225",
            "id,date,days_online\nW7-30,2024-01-30,39\nW7-31,2024-01-31,40",
            "id,oil_rate\nW7-30,980\nW7-31,972",
            "0"),
        new("AnomalyDetection", "Anomaly detection", Icons.Material.Filled.Warning,
            "AUC (higher wins)", "auc",
            "AUC — how well your scores rank the anomalies above the normal rows; higher wins.",
            "Flag the rare abnormal rows — sensor spikes, equipment faults, a payroll run off its pattern.",
            "One row per evaluation id with an anomaly score — higher means more anomalous. AUC ranks your scores, so 0-to-1 works and hard 0/1 calls are not required.",
            "id,motor_temp,vibration,current,label\nn1,150,2.1,64,0\nn2,148,2.3,66,0\na1,255,9.4,180,1",
            "id,motor_temp,vibration,current\ne1,152,2.0,63\ne2,262,9.9,190",
            "id,label\ne1,0\ne2,1",
            "0.5"),
    ];

    public static CompetitionTaskOption Find(string? taskType) =>
        All.FirstOrDefault(t => string.Equals(t.Key, taskType, StringComparison.OrdinalIgnoreCase)) ?? All[0];

    /// <summary>The options inside one scorer's family — what the Host console may still move between.</summary>
    public static IReadOnlyList<CompetitionTaskOption> ForScorer(string? scorerCode) =>
        string.IsNullOrWhiteSpace(scorerCode)
            ? All
            : [.. All.Where(t => t.Scorer.Equals(scorerCode, StringComparison.OrdinalIgnoreCase))];

    /// <summary>
    /// Every reader-facing string above, for the localization coverage test — these reach markup as
    /// computed keys (<c>L[option.Label]</c>), which the literal scan cannot see. The CSV sketches stay
    /// out: they are column names and data, the same in both languages.
    /// </summary>
    public static readonly string[] Translatable =
        [.. All.SelectMany(t => new[] { t.Label, t.Metric, t.MetricPlain, t.Help, t.SubmissionHint }).Distinct()];
}
