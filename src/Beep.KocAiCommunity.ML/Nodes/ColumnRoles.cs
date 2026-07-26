using Microsoft.ML;

namespace Beep.KocAiCommunity.ML.Nodes;

/// <summary>
/// The role each column plays in a run — the platform's single, first-class answer to "which column is
/// the target, which is the id, which are the features", analogous to the <c>X</c> / <c>y</c> split of a
/// typical ML workflow. The data still flows as one wide <see cref="PipelineTable"/> (the ML.NET convention
/// of a single view addressed by column name); <see cref="ColumnRoles"/> is the metadata that names the parts,
/// resolved from the run's label/id and the table's columns.
/// <list type="bullet">
///   <item><description><see cref="Features"/> — the model inputs (<c>X</c>): every column that isn't a reserved role, in table order.</description></item>
///   <item><description><see cref="Label"/> — the target (<c>y</c>).</description></item>
///   <item><description><see cref="Id"/> — the row key; carried through for prediction alignment, never a feature.</description></item>
///   <item><description><see cref="Fold"/> — the internal train/test membership marker.</description></item>
///   <item><description><see cref="Weight"/>, <see cref="Group"/> — reserved for weighted / ranking runs (unused today, but excluded from features so wiring them later is additive).</description></item>
/// </list>
/// </summary>
public sealed record ColumnRoles(
    string Label,
    string? Id,
    string Fold,
    IReadOnlyList<string> Features,
    string? Weight = null,
    string? Group = null)
{
    /// <summary>Whether an id column is designated (present as a role).</summary>
    public bool HasId => !string.IsNullOrEmpty(Id);

    /// <summary>
    /// Resolves the roles for a set of columns: <c>X</c> (<see cref="Features"/>) is every column that is not
    /// a reserved role (label, id, fold, and — when set — weight/group), preserving the column order.
    /// </summary>
    public static ColumnRoles Resolve(
        string label,
        string? id,
        IReadOnlyList<string> columns,
        string fold = PipelineContext.FoldColumn,
        string? weight = null,
        string? group = null)
    {
        var reserved = new HashSet<string>(StringComparer.Ordinal) { label, fold };
        if (!string.IsNullOrEmpty(id)) { reserved.Add(id); }
        if (!string.IsNullOrEmpty(weight)) { reserved.Add(weight); }
        if (!string.IsNullOrEmpty(group)) { reserved.Add(group); }

        var features = columns.Where(c => !reserved.Contains(c)).ToList();
        return new ColumnRoles(label, id, fold, features, weight, group);
    }

    /// <summary>The numeric feature columns (<c>X</c> ∩ numeric) per the given ML.NET schema.</summary>
    public string[] NumericFeatures(DataViewSchema schema) => PipelineContext.NumericFeatures(schema, Features);

    /// <summary>The text feature columns (<c>X</c> ∩ text) per the given ML.NET schema.</summary>
    public string[] TextFeatures(DataViewSchema schema) => PipelineContext.TextFeatures(schema, Features);

    /// <summary>
    /// Materializes the explicit <c>X</c> / <c>y</c> split of a loaded view: a features-only view and a
    /// single-column label view — the shape a regular ML workflow works in. The engine itself trains on the
    /// combined view (ML.NET addresses columns by name, so it never needs the split); this is for callers
    /// that want to inspect, export, or reason over the features and the target separately. Only roles that
    /// are actually present in the view are selected, so it is safe to call at any point in the pipeline.
    /// </summary>
    public (IDataView X, IDataView Y) Split(MLContext ml, IDataView view)
    {
        var present = view.Schema.Where(c => !c.IsHidden).Select(c => c.Name).ToHashSet(StringComparer.Ordinal);
        var featureCols = Features.Where(present.Contains).ToArray();
        var labelCols = present.Contains(Label) ? new[] { Label } : [];

        var x = ml.Transforms.SelectColumns(featureCols).Fit(view).Transform(view);
        var y = ml.Transforms.SelectColumns(labelCols).Fit(view).Transform(view);
        return (x, y);
    }
}
