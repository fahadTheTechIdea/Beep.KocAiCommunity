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
            "sql", "sql-filter", "group-by", "sort", "distinct", "join-dataset", "union-dataset",
            "split", "time-split", "train", "cross-validate", "cluster", "score", "evaluate",
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

        // The executor threads ONE table linearly through the topological order, so a valid graph must be
        // a single chain: no node may take input from two producers (fan-in) or feed two consumers
        // (fan-out) — either would silently mis-thread the wrong table into a node. A join/union node's
        // second dataset arrives via a side channel (its datasetId parameter), not a graph edge, so those
        // still have in-degree 1 and are unaffected.
        var links = definition.Edges
            .Where(e => nodeIds.Contains(e.FromNodeId) && nodeIds.Contains(e.ToNodeId))
            .Select(e => (e.FromNodeId, e.ToNodeId))
            .Distinct()
            .ToList();

        foreach (var group in links.GroupBy(l => l.ToNodeId).Where(g => g.Count() > 1).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            errors.Add($"Node '{group.Key}' has {group.Count()} inputs — a pipeline must be a single chain (no branches). "
                + "Remove the extra connection, or join a second dataset with a 'join-dataset'/'union-dataset' node.");
        }

        foreach (var group in links.GroupBy(l => l.FromNodeId).Where(g => g.Count() > 1).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            errors.Add($"Node '{group.Key}' feeds {group.Count()} nodes — a pipeline must be a single chain (no branches). "
                + "Keep one path from the dataset to the model.");
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
