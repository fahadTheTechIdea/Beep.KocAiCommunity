using System.Globalization;
using Beep.KocAiCommunity.Application.ML;

namespace Beep.KocAiCommunity.ML.Nodes;

/// <summary>
/// The node catalog, derived from the registered <see cref="IPipelineNodeHandler"/>s — a single source
/// of truth for the API catalog, parameter validation, and the executor's dispatch. Adding a node is
/// one handler class registered in DI; the catalog and executor pick it up automatically.
/// </summary>
public sealed class PluginNodeRegistry : INodeRegistry
{
    private static readonly string[] CategoryOrder =
        ["Source", "Transform", "Prepare", "Shape", "Data", "SQL", "Split", "Model", "Evaluate"];

    private readonly Dictionary<string, IPipelineNodeHandler> _handlers;
    private readonly IReadOnlyList<NodeDescriptor> _nodes;

    public PluginNodeRegistry(IEnumerable<IPipelineNodeHandler> handlers)
    {
        _handlers = handlers.ToDictionary(h => h.Descriptor.Kind, StringComparer.OrdinalIgnoreCase);
        _nodes = [.. _handlers.Values.Select(h => h.Descriptor)
            .OrderBy(n => Array.IndexOf(CategoryOrder, n.Category) is var i && i >= 0 ? i : int.MaxValue)
            .ThenBy(n => n.DisplayName, StringComparer.Ordinal)];
    }

    public IReadOnlyList<NodeDescriptor> All => _nodes;
    public IReadOnlyList<string> Categories => _nodes.Select(n => n.Category).Distinct().ToList();
    public NodeDescriptor? Find(string kind) => _handlers.TryGetValue(kind, out var h) ? h.Descriptor : null;

    /// <summary>The handler for a node kind (used by the executor to dispatch).</summary>
    public IPipelineNodeHandler? Handler(string kind) => _handlers.GetValueOrDefault(kind);

    /// <summary>Every known node kind — the compiler validates graphs against this.</summary>
    public IReadOnlyCollection<string> Kinds => _handlers.Keys;

    public ParameterValidation ValidateParameters(string kind, IReadOnlyDictionary<string, string> config)
    {
        var descriptor = Find(kind);
        if (descriptor is null)
        {
            return new ParameterValidation(false, [$"Unknown node kind '{kind}'."]);
        }

        var errors = new List<string>();
        foreach (var p in descriptor.Parameters)
        {
            config.TryGetValue(p.Name, out var raw);
            var provided = !string.IsNullOrWhiteSpace(raw);

            if (p.Required && !provided)
            {
                errors.Add($"'{p.DisplayName}' is required.");
                continue;
            }

            if (!provided)
            {
                continue;
            }

            if (p.Type == NodeParameterType.Number && !double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out _))
            {
                errors.Add($"'{p.DisplayName}' must be a number.");
            }

            if (p.Type == NodeParameterType.Select && p.Options is { Count: > 0 } && !p.Options.Contains(raw))
            {
                errors.Add($"'{p.DisplayName}' must be one of: {string.Join(", ", p.Options)}.");
            }
        }

        return new ParameterValidation(errors.Count == 0, errors);
    }
}
