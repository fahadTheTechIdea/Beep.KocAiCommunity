using System.Text.Json;
using Beep.KocAiCommunity.Application.Jobs;
using Beep.KocAiCommunity.Application.ML;
using Beep.KocAiCommunity.Application.Storage;
using Beep.KocAiCommunity.Application.Studio;
using Beep.KocAiCommunity.Contracts.Jobs;
using Beep.KocAiCommunity.Domain.Datasets;
using Beep.KocAiCommunity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Beep.KocAiCommunity.Infrastructure.Jobs;

/// <summary>
/// Runs a <c>model.train</c> job: trains an ML.NET AutoML model from a stored dataset artifact and
/// records the resulting <see cref="Domain.Studio.ModelRun"/>. Reuses the same training path as the
/// synchronous Studio flow, but durably and out-of-band.
/// </summary>
public sealed class ModelTrainingJobHandler(KocDbContext db, IArtifactService artifacts, IStudioService studio) : IJobHandler
{
    public string JobType => JobTypes.ModelTrain;

    public async Task ExecuteAsync(JobExecutionContext context, CancellationToken ct)
    {
        var payload = JsonSerializer.Deserialize<ModelTrainPayload>(context.PayloadJson)
            ?? throw new InvalidOperationException("Invalid model.train payload.");

        await context.LogAsync($"Loading dataset {payload.DatasetId}…");
        var dataset = await db.Set<Dataset>().AsNoTracking().FirstOrDefaultAsync(d => d.Id == payload.DatasetId, ct)
            ?? throw new InvalidOperationException("Dataset not found.");

        if (dataset.FileArtifactId is null)
        {
            throw new InvalidOperationException("This dataset has no file to train on.");
        }

        if (!Enum.TryParse<MlTaskType>(payload.TaskType, ignoreCase: true, out var task))
        {
            task = MlTaskType.BinaryClassification;
        }

        ct.ThrowIfCancellationRequested();
        await context.LogAsync("Opening dataset file…");
        await using var csv = await artifacts.OpenReadAsync(dataset.FileArtifactId.Value, ct);

        var maxSeconds = payload.MaxSeconds <= 0 ? 30 : payload.MaxSeconds;
        await context.LogAsync($"Training ({task}) on '{dataset.Name}' — up to {maxSeconds}s…");
        var run = await studio.TrainAsync(payload.OwnerUserId, dataset.Name, payload.LabelColumn, task, csv, maxSeconds, ct);

        await context.LogAsync($"Done: {run.Algorithm} · {run.PrimaryMetric}={run.PrimaryValue:0.###} on {run.RowCount} rows.");
    }
}
