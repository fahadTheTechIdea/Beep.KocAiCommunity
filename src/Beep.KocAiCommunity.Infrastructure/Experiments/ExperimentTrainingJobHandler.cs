using System.Text.Json;
using Beep.KocAiCommunity.Application.Experiments;
using Beep.KocAiCommunity.Application.Jobs;
using Beep.KocAiCommunity.Application.ML;
using Beep.KocAiCommunity.Application.Storage;
using Beep.KocAiCommunity.Contracts.Experiments;
using Beep.KocAiCommunity.Contracts.Jobs;
using Beep.KocAiCommunity.Domain.Datasets;
using Beep.KocAiCommunity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Beep.KocAiCommunity.Infrastructure.Experiments;

/// <summary>
/// Runs an <c>experiment.train</c> job: trains an ML.NET AutoML model while streaming each trial's
/// metric into the experiment tracker (via a non-blocking channel), then finalizes the run with its
/// summary metrics and reproducibility snapshot. Best-run selection updates automatically.
/// </summary>
public sealed class ExperimentTrainingJobHandler(
    KocDbContext db,
    IArtifactService artifacts,
    IMlTrainer trainer,
    IExperimentService experiments) : IJobHandler
{
    public string JobType => JobTypes.ExperimentTrain;

    // Maps each AutoML trial to a metric entry and publishes it without blocking training.
    private sealed class ChannelReporter(BoundedMetricChannel channel) : IProgress<TrialReport>
    {
        private int _count;
        public int Count => _count;

        public void Report(TrialReport value)
        {
            Interlocked.Increment(ref _count);
            channel.TryPublish(new RunMetricEntry(value.MetricName, value.MetricValue, "validation", "trial", value.TrialNumber));
        }
    }

    public async Task ExecuteAsync(JobExecutionContext context, CancellationToken ct)
    {
        var payload = JsonSerializer.Deserialize<ExperimentTrainPayload>(context.PayloadJson)
            ?? throw new InvalidOperationException("Invalid experiment.train payload.");

        var experimentId = payload.ExperimentId
            ?? (await experiments.CreateAsync(payload.OwnerUserId,
                new CreateExperimentRequest(string.IsNullOrWhiteSpace(payload.ExperimentName) ? "Experiment" : payload.ExperimentName, "", null, null), ct)).Id;

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

        var runId = await experiments.StartRunAsync(new StartRunRequest(experimentId, payload.OwnerUserId, task.ToString(), payload.DatasetId, null), ct);
        await context.LogAsync($"Run started; training ({task}) on '{dataset.Name}' up to {payload.MaxSeconds}s…");

        // Non-blocking metric pipeline: trials → bounded channel → batched DB writes.
        var channel = new BoundedMetricChannel();
        var reporter = new ChannelReporter(channel);
        var drain = Task.Run(() => channel.DrainAsync(
            batch => experiments.LogMetricsAsync(runId, batch, CancellationToken.None), batchSize: 16, CancellationToken.None), CancellationToken.None);

        TrainingResult result;
        try
        {
            await using var csv = await artifacts.OpenReadAsync(dataset.FileArtifactId.Value, ct);
            result = await trainer.TrainWithTrialsAsync(task, csv, payload.LabelColumn, payload.MaxSeconds <= 0 ? 30 : payload.MaxSeconds, reporter, ct);
        }
        catch
        {
            channel.Complete();
            await drain;
            await experiments.FinishRunAsync(runId, new FinishRunRequest("failed", null, null, null, null, null, 0, reporter.Count, null, null, null, "training"), ct);
            throw;
        }

        channel.Complete();
        await drain;

        var environment = JsonSerializer.Serialize(new
        {
            framework = "ML.NET AutoML",
            seed = 1,
            os = Environment.OSVersion.ToString(),
            dotnet = Environment.Version.ToString(),
        });
        var hyperparameters = JsonSerializer.Serialize(new { trainer = result.Algorithm, maxSeconds = payload.MaxSeconds, task = task.ToString() });

        await experiments.FinishRunAsync(runId, new FinishRunRequest(
            "completed", result.Algorithm, result.PrimaryMetric, result.PrimaryValue, result.SecondaryMetric, result.SecondaryValue,
            result.RowCount, reporter.Count, hyperparameters, environment, dataset.FileArtifactId.Value.ToString("N"), null), ct);

        await context.LogAsync($"Done: {result.Algorithm} · {result.PrimaryMetric}={result.PrimaryValue:0.###} over {reporter.Count} trial(s).");
    }
}
