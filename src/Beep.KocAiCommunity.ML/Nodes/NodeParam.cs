using Beep.KocAiCommunity.Application.ML;

namespace Beep.KocAiCommunity.ML.Nodes;

/// <summary>Terse builder for a node's typed parameter (mirrors the old catalog's <c>P(...)</c> helper).</summary>
internal static class NodeParam
{
    public static NodeParameter P(string name, string display, NodeParameterType type, bool required = false, string? def = null, string[]? options = null)
        => new(name, display, type, required, def, options);
}
