using Beep.KocAiCommunity.Contracts.Workflow;

namespace Beep.KocAiCommunity.Application.Workflow;

/// <summary>
/// Finds the secondary datasets a workflow references (join/union nodes carry a <c>datasetId</c>
/// config), so a caller can resolve each to its stored CSV and pass them to the executor.
/// </summary>
public static class WorkflowDatasetScanner
{
    public const string DatasetIdKey = "datasetId";

    public static IReadOnlyList<Guid> ReferencedDatasetIds(WorkflowDefinition definition)
    {
        var ids = new List<Guid>();
        foreach (var node in definition.Nodes)
        {
            if (node.Config is not null
                && node.Config.TryGetValue(DatasetIdKey, out var raw)
                && Guid.TryParse(raw, out var id)
                && !ids.Contains(id))
            {
                ids.Add(id);
            }
        }

        return ids;
    }
}
