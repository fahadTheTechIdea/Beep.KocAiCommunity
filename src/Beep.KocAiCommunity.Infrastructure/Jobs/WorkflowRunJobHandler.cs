using System.Text.Json;
using Beep.KocAiCommunity.Application.Experiments;
using Beep.KocAiCommunity.Application.Jobs;
using Beep.KocAiCommunity.Application.ML;
using Beep.KocAiCommunity.Application.Storage;
using Beep.KocAiCommunity.Contracts.Experiments;
using Beep.KocAiCommunity.Contracts.Jobs;
using Beep.KocAiCommunity.Domain.Common;
using Beep.KocAiCommunity.Domain.Datasets;
using Beep.KocAiCommunity.Domain.Studio;
using Beep.KocAiCommunity.Infrastructure.Experiments;
using Beep.KocAiCommunity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Beep.KocAiCommunity.Infrastructure.Jobs;

/// <summary>
/// Runs a <c>workflow.run</c> job: trains a published workflow version against a dataset with live
/// trial tracking into a new experiment, then records a registerable <see cref="ModelRun"/> (with its
/// saved model artifact) so the result can be promoted through the model registry. This is the durable
/// bridge that gives a published workflow a purpose: Run → Experiment (trials) → ModelRun → Models.
/// </summary>
public sealed class WorkflowRunJobHandler(
    KocDbContext db,
    IArtifactService artifacts,
    IMlTrainer trainer,
    IExperimentService experiments) : IJobHandler
{
    public string JobType => JobTypes.WorkflowRun;

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
        var payload = JsonSerializer.Deserialize<WorkflowRunPayload>(context.PayloadJson)
            ?? throw new InvalidOperationException("Invalid workflow.run payload.");

        var version = await db.Set<WorkflowVersion>().AsNoTracking()
            .FirstOrDefaultAsync(v => v.WorkflowId == payload.WorkflowId && v.VersionNumber == payload.VersionNumber, ct)
            ?? throw new InvalidOperationException("Workflow version not found.");
        if (version.Status != "published")
        {
            throw new InvalidOperationException("Only a published workflow version can be run.");
        }

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

        var experimentId = (await experiments.CreateAsync(payload.OwnerUserId,
            new CreateExperimentRequest($"{payload.WorkflowName} v{payload.VersionNumber}", $"Run of workflow {payload.WorkflowId}", null, null), ct)).Id;
        var runId = await experiments.StartRunAsync(new StartRunRequest(experimentId, payload.OwnerUserId, task.ToString(), payload.DatasetId, null), ct);
        await context.LogAsync($"Running '{payload.WorkflowName}' v{payload.VersionNumber} on '{dataset.Name}' ({task})…");

        // Non-blocking metric pipeline: trials → bounded channel → batched writes.
        var channel = new BoundedMetricChannel();
        var reporter = new ChannelReporter(channel);
        var drain = Task.Run(() => channel.DrainAsync(
            batch => experiments.LogMetricsAsync(runId, batch, CancellationToken.None), batchSize: 16, CancellationToken.None), CancellationToken.None);

        CapturedModel captured;
        try
        {
            await using var csv = await artifacts.OpenReadAsync(dataset.FileArtifactId.Value, ct);
            captured = await trainer.TrainAndCaptureAsync(task, csv, payload.LabelColumn, payload.MaxSeconds <= 0 ? 30 : payload.MaxSeconds, reporter, ct);
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

        var result = captured.Result;

        // Persist the model + record a registerable ModelRun, then link it to the experiment run.
        var modelRun = await Studio.ModelRunRecorder.RecordAsync(db, artifacts, captured, payload.WorkflowName, payload.LabelColumn, dataset.Classification, payload.OwnerUserId, ct);

        var hyperparameters = JsonSerializer.Serialize(new { trainer = result.Algorithm, task = task.ToString(), workflowVersion = payload.VersionNumber });
        var environment = JsonSerializer.Serialize(new { framework = "ML.NET AutoML", seed = 1, dotnet = Environment.Version.ToString() });
        await experiments.FinishRunAsync(runId, new FinishRunRequest(
            "completed", result.Algorithm, result.PrimaryMetric, result.PrimaryValue, result.SecondaryMetric, result.SecondaryValue,
            result.RowCount, reporter.Count, hyperparameters, environment, dataset.FileArtifactId.Value.ToString("N"), null, modelRun.Id), ct);

        await context.LogAsync($"Done: {result.Algorithm} · {result.PrimaryMetric}={result.PrimaryValue:0.###} · ModelRun {modelRun.Id} ready to register.");
    }
}
