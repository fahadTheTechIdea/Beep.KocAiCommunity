using Beep.KocAiCommunity.Contracts.Workflow;
using Beep.KocAiCommunity.Domain.Studio;

namespace Beep.KocAiCommunity.Application.Workflow;

/// <summary>Raised when a workflow cannot run (invalid graph).</summary>
public sealed class WorkflowException(string message) : Exception(message);

/// <summary>Compiles/validates workflows and executes them (the train node runs ML.NET AutoML).</summary>
public interface IWorkflowService
{
    WorkflowValidationResult Validate(WorkflowDefinition definition);

    Task<ModelRun> RunAsync(string userId, WorkflowDefinition definition, string labelColumn, Stream csv, int maxSeconds, CancellationToken ct = default);
}
