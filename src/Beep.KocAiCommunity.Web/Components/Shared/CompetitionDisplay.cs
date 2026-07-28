using Beep.KocAiCommunity.Contracts.Competitions;
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

    public static string TaskLabel(string taskType) => taskType switch
    {
        "BinaryClassification" => "Classification",
        "MulticlassClassification" => "Multiclass",
        "Regression" => "Regression",
        "Forecasting" => "Forecasting",
        _ => taskType,
    };

    /// <summary>"Accuracy" / "RMSE — lower wins", from the enriched DTO (falls back to task inference).</summary>
    public static string MetricLabel(CompetitionDto c)
    {
        var name = string.IsNullOrEmpty(c.MetricName)
            ? (c.TaskType == "Regression" ? "RMSE" : "Accuracy")
            : c.MetricName;
        return c.HigherIsBetter ? name : $"{name} — lower wins";
    }

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
