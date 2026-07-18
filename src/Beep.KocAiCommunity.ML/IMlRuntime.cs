using Beep.KocAiCommunity.Contracts.Workflow;

namespace Beep.KocAiCommunity.ML;

/// <summary>
/// Entry point for executing a compiled workflow through ML.NET. The concrete runtime and node
/// executors build on <see cref="AutoMlTrainer"/>.
/// </summary>
public interface IMlRuntime
{
    Task<bool> CanExecuteAsync(WorkflowDefinition definition, CancellationToken ct = default);
}
