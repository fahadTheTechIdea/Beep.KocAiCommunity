using Microsoft.ML;
using Microsoft.ML.Data;

namespace Beep.KocAiCommunity.ML;

/// <summary>One input column a model expects, and whether it takes numbers or text.</summary>
public sealed record ModelInputColumn(string Name, bool IsNumeric);

/// <summary>
/// Reads what a serialized ML.NET model expects as input.
/// <para>
/// A saved model already carries its own input schema, which makes it the only trustworthy source for
/// "which columns does this need". Recording the feature list at training time and trusting that later
/// would drift the moment anything about featurization changed.
/// </para>
/// </summary>
public static class ModelSchema
{
    /// <summary>The model's non-hidden input columns, in schema order.</summary>
    public static IReadOnlyList<ModelInputColumn> Read(byte[] modelBytes)
    {
        var ml = new MLContext(seed: 1);
        using var stream = new MemoryStream(modelBytes);
        ml.Model.Load(stream, out var schema);

        return [.. schema
            .Where(c => !c.IsHidden)
            .Select(c => new ModelInputColumn(c.Name, IsNumeric(c.Type)))];
    }

    private static bool IsNumeric(DataViewType type) =>
        type.RawType != typeof(ReadOnlyMemory<char>) && type.RawType != typeof(string);
}
