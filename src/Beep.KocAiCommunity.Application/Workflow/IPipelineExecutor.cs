using Beep.KocAiCommunity.Application.ML;
using Beep.KocAiCommunity.Contracts.Workflow;

namespace Beep.KocAiCommunity.Application.Workflow;

/// <summary>
/// Executes an ML pipeline node by node (dataset → transforms → split → train → evaluate),
/// threading the data through each step and reporting per-node status. Binary and regression tasks.
/// </summary>
public interface IPipelineExecutor
{
    Task<PipelineExecutionResult> ExecuteAsync(WorkflowDefinition definition, string labelColumn, MlTaskType task, Stream csv, int maxSeconds, IReadOnlyDictionary<Guid, Stream>? secondaryDatasets = null, CancellationToken ct = default);

    /// <summary>
    /// Trains the pipeline on the full training set, then applies it to an evaluation feature set
    /// (an <c>id</c> column plus the same features, no label) and returns an <c>id,prediction</c> CSV
    /// suitable for competition scoring. Used to turn a Studio pipeline into a leaderboard submission.
    /// </summary>
    Task<string> PredictAsync(WorkflowDefinition definition, string labelColumn, string idColumn, MlTaskType task, Stream trainingCsv, Stream evaluationCsv, IReadOnlyDictionary<Guid, Stream>? secondaryDatasets = null, CancellationToken ct = default);
}
