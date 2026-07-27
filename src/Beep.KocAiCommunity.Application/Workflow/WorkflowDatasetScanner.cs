using Beep.KocAiCommunity.Contracts.Workflow;

namespace Beep.KocAiCommunity.Application.Workflow;

/// <summary>
/// Finds the <em>secondary</em> datasets a workflow references — the join/union nodes carry a
/// <c>datasetId</c> config, so a caller can resolve each to its stored CSV and pass them to the executor.
/// The primary <c>dataset</c> source node also carries a <c>datasetId</c>, but that is the pipeline's main
/// input (opened as the training stream), not a secondary, so it is deliberately excluded here.
/// </summary>
public static class WorkflowDatasetScanner
{
    public const string DatasetIdKey = "datasetId";

    /// <summary>Node kinds that reference a <em>secondary</em> dataset (not the primary source node).</summary>
    private static readonly HashSet<string> SecondaryDatasetKinds =
        new(["join-dataset", "union-dataset"], StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<Guid> ReferencedDatasetIds(WorkflowDefinition definition)
    {
        var ids = new List<Guid>();
        foreach (var node in definition.Nodes)
        {
            if (SecondaryDatasetKinds.Contains(node.Kind)
                && node.Config is not null
                && node.Config.TryGetValue(DatasetIdKey, out var raw)
                && Guid.TryParse(raw, out var id)
                && !ids.Contains(id))
            {
                ids.Add(id);
            }
        }

        return ids;
    }

    /// <summary>The primary <c>dataset</c> source node's selected dataset id, if any.</summary>
    public static Guid? PrimaryDatasetId(WorkflowDefinition definition)
    {
        foreach (var node in definition.Nodes)
        {
            if (string.Equals(node.Kind, "dataset", StringComparison.OrdinalIgnoreCase)
                && node.Config is not null
                && node.Config.TryGetValue(DatasetIdKey, out var raw)
                && Guid.TryParse(raw, out var id))
            {
                return id;
            }
        }

        return null;
    }
}
