using Beep.KocAiCommunity.Application.ML;
using Beep.KocAiCommunity.Contracts.Workflow;
using Beep.KocAiCommunity.Domain.Studio;

namespace Beep.KocAiCommunity.Application.Workflow;

/// <summary>Raised when a workflow cannot run (invalid graph, or a node failed).</summary>
public sealed class WorkflowException(string message) : Exception(message);

/// <summary>Compiles/validates workflows and runs them by executing the actual node graph.</summary>
public interface IWorkflowService
{
    WorkflowValidationResult Validate(WorkflowDefinition definition);

    Task<ModelRun> RunAsync(string userId, WorkflowDefinition definition, string labelColumn, MlTaskType task, Stream csv, int maxSeconds, IReadOnlyDictionary<Guid, Stream>? secondaryDatasets = null, CancellationToken ct = default);
}
