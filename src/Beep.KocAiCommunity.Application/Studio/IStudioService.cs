using Beep.KocAiCommunity.Application.ML;
using Beep.KocAiCommunity.Domain.Studio;

namespace Beep.KocAiCommunity.Application.Studio;

/// <summary>Trains an ML.NET model from an uploaded CSV and records the run.</summary>
public interface IStudioService
{
    Task<ModelRun> TrainAsync(string userId, string datasetName, string labelColumn, MlTaskType task, Stream csv, int maxSeconds, CancellationToken ct = default);
    Task<IReadOnlyList<ModelRun>> GetMyRunsAsync(string userId, CancellationToken ct = default);
}
