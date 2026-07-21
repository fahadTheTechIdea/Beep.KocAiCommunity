using MudBlazor;

namespace Beep.KocAiCommunity.Ui.Studio.Diagrams;

/// <summary>
/// Presentation only. The node catalog itself — kinds, categories, parameters — is fetched live
/// from the backend registry (<c>GET /api/v1/ml/nodes</c>), so a new handler on the server appears
/// in the designer with no client change. This maps a kind/category to an icon and colour, the one
/// thing the API does not carry (MudBlazor icon constants live in the Web layer).
/// </summary>
public static class NodeVisuals
{
    // Palette order that reads like a pipeline flow; categories not listed here fall to the end.
    private static readonly string[] CategoryOrder =
        ["Source", "Data", "Prepare", "Shape", "Transform", "Reduce", "Encode", "Clean", "Scale", "Split", "Model", "Evaluate"];

    private static readonly Dictionary<string, string> CategoryColor = new(StringComparer.Ordinal)
    {
        ["Source"] = "var(--koc-accent-700)",
        ["Data"] = "#4f46e5",        // DuckDB / SQL — indigo
        ["Prepare"] = "#0d9488",     // teal
        ["Shape"] = "#475569",       // slate
        ["Transform"] = "#b45309",   // amber
        ["Split"] = "#0369a1",
        ["Model"] = "#15803d",       // green
        ["Evaluate"] = "#475569",
    };

    private static readonly Dictionary<string, string> KindIcon = new(StringComparer.Ordinal)
    {
        // Source / Data (DuckDB SQL + ETL)
        ["dataset"] = Icons.Material.Filled.TableChart,
        ["sql"] = Icons.Material.Filled.Code,
        ["sql-filter"] = Icons.Material.Filled.FilterAlt,
        ["group-by"] = Icons.Material.Filled.Analytics,
        ["sort"] = Icons.Material.Filled.Sort,
        ["distinct"] = Icons.Material.Filled.Deblur,
        ["join-dataset"] = Icons.Material.Filled.CallMerge,
        ["union-dataset"] = Icons.Material.Filled.LibraryAdd,
        // Prepare
        ["rename-column"] = Icons.Material.Filled.DriveFileRenameOutline,
        ["convert-numeric"] = Icons.Material.Filled.Numbers,
        ["compute-column"] = Icons.Material.Filled.Functions,
        ["combine-columns"] = Icons.Material.Filled.MergeType,
        ["lp-normalize"] = Icons.Material.Filled.Straighten,
        ["global-contrast"] = Icons.Material.Filled.Contrast,
        // Shape (row ops)
        ["take-rows"] = Icons.Material.Filled.VerticalAlignTop,
        ["shuffle"] = Icons.Material.Filled.Shuffle,
        ["sample"] = Icons.Material.Filled.Casino,
        ["filter-rows"] = Icons.Material.Filled.FilterAlt,
        // Transform (feature engineering)
        ["select-columns"] = Icons.Material.Filled.Checklist,
        ["drop-columns"] = Icons.Material.Filled.LayersClear,
        ["standardize"] = Icons.Material.Filled.Functions,
        ["normalize"] = Icons.Material.Filled.Straighten,
        ["log-normalize"] = Icons.Material.Filled.ShowChart,
        ["robust-scale"] = Icons.Material.Filled.Insights,
        ["binning"] = Icons.Material.Filled.BarChart,
        ["replace-missing"] = Icons.Material.Filled.HealthAndSafety,
        ["one-hot"] = Icons.Material.Filled.GridOn,
        ["hash-encode"] = Icons.Material.Filled.Tag,
        ["featurize-text"] = Icons.Material.Filled.TextFields,
        ["pca"] = Icons.Material.Filled.ScatterPlot,
        ["feature-selection"] = Icons.Material.Filled.FilterList,
        // Split / Model / Evaluate
        ["split"] = Icons.Material.Filled.CallSplit,
        ["train"] = Icons.Material.Filled.ModelTraining,
        ["cluster"] = Icons.Material.Filled.BubbleChart,
        ["cross-validate"] = Icons.Material.Filled.Loop,
        ["score"] = Icons.Material.Filled.Calculate,
        ["evaluate"] = Icons.Material.Filled.Assessment,
    };

    private static readonly Dictionary<string, string> CategoryIcon = new(StringComparer.Ordinal)
    {
        ["Source"] = Icons.Material.Filled.TableChart,
        ["Data"] = Icons.Material.Filled.Storage,
        ["Prepare"] = Icons.Material.Filled.CleaningServices,
        ["Shape"] = Icons.Material.Filled.Transform,
        ["Transform"] = Icons.Material.Filled.AutoFixHigh,
        ["Split"] = Icons.Material.Filled.CallSplit,
        ["Model"] = Icons.Material.Filled.ModelTraining,
        ["Evaluate"] = Icons.Material.Filled.Assessment,
    };

    /// <summary>Icon for a node kind, falling back to its category, then a generic widget.</summary>
    public static string Icon(string kind, string category) =>
        KindIcon.TryGetValue(kind, out var i) ? i
        : CategoryIcon.TryGetValue(category, out var ci) ? ci
        : Icons.Material.Filled.Widgets;

    /// <summary>Accent colour for a category (slate fallback).</summary>
    public static string Color(string category) =>
        CategoryColor.TryGetValue(category, out var c) ? c : "#475569";

    /// <summary>Sort key so the palette lists categories in pipeline order; unknown = last.</summary>
    public static int Order(string category)
    {
        var idx = Array.IndexOf(CategoryOrder, category);
        return idx < 0 ? int.MaxValue : idx;
    }
}
