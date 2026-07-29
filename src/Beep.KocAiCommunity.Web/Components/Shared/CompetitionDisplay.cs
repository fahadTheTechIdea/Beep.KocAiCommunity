using Beep.KocAiCommunity.Contracts.Competitions;
using Beep.KocAiCommunity.Ui.Shared.Components;
using Beep.KocAiCommunity.Ui.Shared.Localization;
using Microsoft.Extensions.Localization;
using MudBlazor;

namespace Beep.KocAiCommunity.Web.Components.Shared;

/// <summary>Display helpers shared by the arena grid, cards, hero, and the competition detail page.</summary>
public static class CompetitionDisplay
{
    public static Color StatusColor(string status) => status switch
    {
        "active" => Color.Success,
        "concluded" => Color.Warning,
        _ => Color.Default,
    };

    /// <summary>Every label this class can return, for the localization coverage test.</summary>
    public static readonly string[] Translatable =
        ["Draft", "Active", "Concluded", "Team", "Group", "Directorate", "Company", "{0} — lower wins"];

    public static string TaskLabel(string taskType) => MlTaskLabels.Display(taskType);

    /// <summary>The stored lifecycle code as a word. Codes are lowercase on the wire, words are not.</summary>
    public static string StatusLabel(string status) => status switch
    {
        "draft" => "Draft",
        "active" => "Active",
        "concluded" => "Concluded",
        _ => status,
    };

    /// <summary>Who can see it — the VisibilityScope name, which is already a word.</summary>
    public static string ScopeLabel(string scope) => scope;

    /// <summary>
    /// The metric's name, from the enriched DTO (falls back to task inference). The name itself is not
    /// translated — RMSE and AUC are RMSE and AUC in any language — but the "lower wins" qualifier is,
    /// so callers compose it through the localizer rather than getting a half-English sentence here.
    /// </summary>
    public static string MetricName(CompetitionDto c) =>
        string.IsNullOrEmpty(c.MetricName)
            ? (c.TaskType == "Regression" ? "RMSE" : "Accuracy")
            : c.MetricName;

    /// <summary>True when a lower score is a better score, so the caller can say so.</summary>
    public static bool LowerWins(CompetitionDto c) => !c.HigherIsBetter;

    /// <summary>
    /// The metric as it reads on a chip: "AUC", or "RMSE — lower wins". Takes the localizer rather than
    /// returning a half-translated sentence, so the four places that show this stay identical.
    /// </summary>
    public static string MetricLabel(CompetitionDto c, IStringLocalizer<Strings> l) =>
        LowerWins(c) ? l["{0} — lower wins", MetricName(c)] : MetricName(c);

    /// <summary>Fraction (0..1) of the competition's life elapsed toward its final reveal, or null.</summary>
    public static double? RevealProgress(CompetitionDto c, DateTime utcNow)
    {
        if (c.CreatedUtc is not { } start || c.RevealUtc is not { } end || end <= start)
        {
            return null;
        }

        var fraction = (utcNow - start).TotalSeconds / (end - start).TotalSeconds;
        return Math.Clamp(fraction, 0, 1);
    }
}
