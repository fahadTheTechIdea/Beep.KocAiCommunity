using Beep.KocAiCommunity.Contracts.Workflow;

namespace Beep.KocAiCommunity.Workflow;

/// <summary>
/// Validates and topologically orders a <see cref="WorkflowDefinition"/>: a workflow must have a
/// dataset source and a train node, reference only existing nodes, and be acyclic. Uses Kahn's
/// algorithm (processing ready nodes in id order for a deterministic plan).
/// </summary>
public static class WorkflowCompiler
{
    public static readonly IReadOnlySet<string> KnownKinds =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "dataset", "select-columns", "drop-columns", "sample", "filter-rows",
            "one-hot", "hash-encode", "featurize-text", "replace-missing",
            "normalize", "standardize", "log-normalize", "robust-scale", "binning",
            "pca", "feature-selection",
            "rename-column", "convert-numeric", "compute-column", "combine-columns",
            "lp-normalize", "global-contrast", "take-rows", "shuffle",
            "split", "train", "cross-validate", "cluster", "score", "evaluate",
        };

    public static WorkflowValidationResult Compile(WorkflowDefinition definition)
    {
        var errors = new List<string>();
        var nodeIds = definition.Nodes.Select(n => n.Id).ToHashSet(StringComparer.Ordinal);

        if (nodeIds.Count != definition.Nodes.Count)
        {
            errors.Add("Node ids must be unique.");
        }

        foreach (var node in definition.Nodes.Where(n => !KnownKinds.Contains(n.Kind)))
        {
            errors.Add($"Node '{node.Id}': unknown kind '{node.Kind}'.");
        }

        foreach (var edge in definition.Edges)
        {
            if (!nodeIds.Contains(edge.FromNodeId) || !nodeIds.Contains(edge.ToNodeId))
            {
                errors.Add($"Edge {edge.FromNodeId}→{edge.ToNodeId} references a node that does not exist.");
            }
        }

        if (!definition.Nodes.Any(n => string.Equals(n.Kind, "dataset", StringComparison.OrdinalIgnoreCase)))
        {
            errors.Add("A workflow needs a dataset source node.");
        }

        if (!definition.Nodes.Any(n => n.Kind is "train" or "cluster" || string.Equals(n.Kind, "train", StringComparison.OrdinalIgnoreCase)))
        {
            errors.Add("A workflow needs a model node (train or cluster).");
        }

        var order = TopologicalOrder(definition, nodeIds, out var hasCycle);
        if (hasCycle)
        {
            errors.Add("The workflow has a cycle.");
        }

        return new WorkflowValidationResult(errors.Count == 0, order, errors);
    }

    private static List<string> TopologicalOrder(WorkflowDefinition definition, HashSet<string> nodeIds, out bool hasCycle)
    {
        var inDegree = definition.Nodes.ToDictionary(n => n.Id, _ => 0, StringComparer.Ordinal);
        var adjacency = definition.Nodes.ToDictionary(n => n.Id, _ => new List<string>(), StringComparer.Ordinal);

        foreach (var edge in definition.Edges)
        {
            if (nodeIds.Contains(edge.FromNodeId) && nodeIds.Contains(edge.ToNodeId))
            {
                adjacency[edge.FromNodeId].Add(edge.ToNodeId);
                inDegree[edge.ToNodeId]++;
            }
        }

        // Deterministic frontier: always take the smallest ready node id.
        var ready = new SortedSet<string>(inDegree.Where(kv => kv.Value == 0).Select(kv => kv.Key), StringComparer.Ordinal);
        var order = new List<string>(definition.Nodes.Count);

        while (ready.Count > 0)
        {
            var id = ready.Min!;
            ready.Remove(id);
            order.Add(id);

            foreach (var next in adjacency[id])
            {
                if (--inDegree[next] == 0)
                {
                    ready.Add(next);
                }
            }
        }

        hasCycle = order.Count != definition.Nodes.Count;
        return order;
    }
}
