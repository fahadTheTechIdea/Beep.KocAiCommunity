using Beep.KocAiCommunity.Contracts.Workflow;

namespace Beep.KocAiCommunity.Contracts.Studio;

/// <summary>Train an AutoML model directly from a catalog dataset (instead of an uploaded CSV).</summary>
public sealed record TrainFromDatasetRequest(Guid DatasetId, string LabelColumn, string Task);

/// <summary>Run a workflow pipeline node-by-node against a catalog dataset.</summary>
public sealed record ExecuteFromDatasetRequest(Guid DatasetId, string LabelColumn, string? Task, WorkflowDefinition Definition);
