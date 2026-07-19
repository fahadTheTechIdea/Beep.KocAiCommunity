using Beep.KocAiCommunity.Contracts.Workflow;

namespace Beep.KocAiCommunity.Application.ML;

/// <summary>
/// Enforces the "split before fit" rule: a supervised model must not be reachable from a dataset
/// without a train/test split in between, otherwise it would be fit on the train+test union (leakage).
/// Operates on the workflow graph; unsupervised <c>cluster</c> nodes are exempt.
/// </summary>
public static class FeaturizationGuard
{
    private static readonly HashSet<string> SupervisedModelKinds = new(StringComparer.OrdinalIgnoreCase) { "train", "cross-validate" };

    public static IReadOnlyList<string> Check(WorkflowDefinition definition)
    {
        var kindById = definition.Nodes.ToDictionary(n => n.Id, n => n.Kind, StringComparer.Ordinal);
        var adjacency = definition.Nodes.ToDictionary(n => n.Id, _ => new List<string>(), StringComparer.Ordinal);
        foreach (var edge in definition.Edges)
        {
            if (adjacency.TryGetValue(edge.FromNodeId, out var list) && kindById.ContainsKey(edge.ToNodeId))
            {
                list.Add(edge.ToNodeId);
            }
        }

        var datasets = definition.Nodes.Where(n => IsKind(n.Kind, "dataset")).Select(n => n.Id).ToList();
        var leaks = new HashSet<string>(StringComparer.Ordinal);

        // BFS over (node, passedSplit) states. Reaching a supervised model with passedSplit == false is a leak.
        foreach (var start in datasets)
        {
            var queue = new Queue<(string Node, bool PassedSplit)>();
            var seen = new HashSet<(string, bool)>();
            queue.Enqueue((start, false));
            seen.Add((start, false));

            while (queue.Count > 0)
            {
                var (node, passed) = queue.Dequeue();
                var kind = kindById[node];

                if (SupervisedModelKinds.Contains(kind) && !passed)
                {
                    leaks.Add(node);
                }

                var nextPassed = passed || IsKind(kind, "split");
                foreach (var next in adjacency[node])
                {
                    var state = (next, nextPassed);
                    if (seen.Add(state))
                    {
                        queue.Enqueue(state);
                    }
                }
            }
        }

        return leaks
            .OrderBy(id => id, StringComparer.Ordinal)
            .Select(id => $"Node '{id}' ({kindById[id]}) would be fit without a train/test split — add a 'split' node before it.")
            .ToList();
    }

    private static bool IsKind(string kind, string expected) => string.Equals(kind, expected, StringComparison.OrdinalIgnoreCase);
}
