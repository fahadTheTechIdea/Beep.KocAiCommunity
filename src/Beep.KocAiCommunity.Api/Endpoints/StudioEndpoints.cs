using System.Text.Json;
using Beep.KocAiCommunity.Application.Datasets;
using Beep.KocAiCommunity.Application.ML;
using Beep.KocAiCommunity.Application.Security;
using Beep.KocAiCommunity.Application.Storage;
using Beep.KocAiCommunity.Application.Studio;
using Beep.KocAiCommunity.Application.Workflow;
using Beep.KocAiCommunity.Contracts.Studio;
using Beep.KocAiCommunity.Contracts.Workflow;
using Beep.KocAiCommunity.Domain.Common;
using Beep.KocAiCommunity.Domain.Datasets;
using Beep.KocAiCommunity.Domain.Studio;
using Microsoft.AspNetCore.Mvc;

namespace Beep.KocAiCommunity.Api.Endpoints;

public static class StudioEndpoints
{
    /// <summary>
    /// Seconds AutoML gets per training request, unless configuration says otherwise.
    /// <para>
    /// Short on purpose: this is a call a person waits on with a spinner in front of them. It is a
    /// floor, not a target — AutoML returns the best trial it finished, and eight seconds is enough for
    /// several on a machine that is not busy. On one that is, no trial finishes and ML.NET throws
    /// <c>TimeoutException</c>, which surfaces as "Training failed". Raise
    /// <c>Studio:TrainingSeconds</c> on a loaded host.
    /// </para>
    /// </summary>
    private const int DefaultTrainingSeconds = 8;

    private static int TrainingSeconds(IConfiguration configuration) =>
        Math.Clamp(configuration.GetValue("Studio:TrainingSeconds", DefaultTrainingSeconds), 1, 600);

    public static RouteGroupBuilder MapStudioEndpoints(this RouteGroupBuilder group)
    {
        // Upload a CSV and train a binary classifier via ML.NET AutoML.
        group.MapPost("/studio/train", async (IFormFile file, string labelColumn, string? datasetName, string? task, IKocCurrentUser me, IStudioService studio, IConfiguration configuration, CancellationToken ct) =>
        {
            if (!Enum.TryParse<MlTaskType>(task, ignoreCase: true, out var taskType))
            {
                taskType = MlTaskType.BinaryClassification;
            }

            try
            {
                await using var stream = file.OpenReadStream();
                var run = await studio.TrainAsync(me.UserId!, string.IsNullOrWhiteSpace(datasetName) ? file.FileName : datasetName, labelColumn, taskType, stream, maxSeconds: TrainingSeconds(configuration), ct: ct);
                return Results.Ok(ToDto(run));
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = $"Training failed: {ex.Message}" });
            }
        })
        .WithName("Train")
        .RequireAuthorization(KocPolicies.RequireEmployee)
        .DisableAntiforgery();

        group.MapGet("/studio/runs", async (IKocCurrentUser me, IStudioService studio, IConfiguration configuration, CancellationToken ct) =>
        {
            var runs = await studio.GetMyRunsAsync(me.UserId!, ct);
            return Results.Ok(runs.Select(ToDto).ToList());
        })
        .WithName("StudioRuns")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        // Compile/validate a workflow graph (source + train, acyclic).
        group.MapPost("/studio/workflows/validate", (WorkflowDefinition definition, IWorkflowService workflow) =>
            Results.Ok(workflow.Validate(definition)))
        .WithName("ValidateWorkflow")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        // Execute a workflow node by node (real ML.NET pipeline: load → transforms → split → train → evaluate).
        group.MapPost("/studio/workflows/execute", async (IFormFile file, [FromForm] string definition, string labelColumn, string? task, IKocCurrentUser me, IDatasetService datasets, IArtifactService artifacts, IPipelineExecutor executor, IConfiguration configuration, CancellationToken ct) =>
        {
            if (!Enum.TryParse<MlTaskType>(task, ignoreCase: true, out var taskType) || taskType == MlTaskType.MulticlassClassification)
            {
                taskType = MlTaskType.BinaryClassification;
            }

            try
            {
                var def = JsonSerializer.Deserialize<WorkflowDefinition>(definition, JsonOptions) ?? new WorkflowDefinition();
                var secondary = await ResolveSecondaryDatasetsAsync(me, datasets, artifacts, def, ct);
                await using var stream = file.OpenReadStream();
                var result = await executor.ExecuteAsync(def, labelColumn, taskType, stream, maxSeconds: TrainingSeconds(configuration), secondaryDatasets: secondary, ct: ct);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = $"Execution failed: {ex.Message}" });
            }
        })
        .WithName("ExecuteWorkflow")
        .RequireAuthorization(KocPolicies.RequireEmployee)
        .DisableAntiforgery();

        // Run a workflow: validates the graph, then executes it node by node (the same engine a
        // competition submission is scored against) and records the run's headline metric.
        group.MapPost("/studio/workflows/run", async (IFormFile file, [FromForm] string definition, string labelColumn, string? task, IKocCurrentUser me, IWorkflowService workflow, IDatasetService datasets, IArtifactService artifacts, IConfiguration configuration, CancellationToken ct) =>
        {
            if (!Enum.TryParse<MlTaskType>(task, ignoreCase: true, out var taskType))
            {
                taskType = MlTaskType.BinaryClassification;
            }

            try
            {
                var def = JsonSerializer.Deserialize<WorkflowDefinition>(definition, JsonOptions) ?? new WorkflowDefinition();
                var secondary = await ResolveSecondaryDatasetsAsync(me, datasets, artifacts, def, ct);
                await using var stream = file.OpenReadStream();
                var run = await workflow.RunAsync(me.UserId!, def, labelColumn, taskType, stream, maxSeconds: TrainingSeconds(configuration), secondaryDatasets: secondary, ct: ct);
                return Results.Ok(ToDto(run));
            }
            catch (WorkflowException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = $"Workflow run failed: {ex.Message}" });
            }
        })
        .WithName("RunWorkflow")
        .RequireAuthorization(KocPolicies.RequireEmployee)
        .DisableAntiforgery();

        // Train AutoML directly from a catalog dataset (no re-upload).
        group.MapPost("/studio/train/dataset", async (TrainFromDatasetRequest req, IKocCurrentUser me, IDatasetService datasets, IArtifactService artifacts, IStudioService studio, IConfiguration configuration, CancellationToken ct) =>
        {
            if (!Enum.TryParse<MlTaskType>(req.Task, ignoreCase: true, out var taskType))
            {
                taskType = MlTaskType.BinaryClassification;
            }

            var access = await ResolveTrainableDatasetAsync(me, datasets, req.DatasetId, ct);
            if (access.Error is not null)
            {
                return access.Error;
            }

            try
            {
                await using var csv = await artifacts.OpenReadAsync(access.Dataset!.FileArtifactId!.Value, ct);
                var run = await studio.TrainAsync(me.UserId!, access.Dataset.Name, req.LabelColumn, taskType, csv, maxSeconds: TrainingSeconds(configuration), ct: ct);
                return Results.Ok(ToDto(run));
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = $"Training failed: {ex.Message}" });
            }
        })
        .WithName("TrainFromDataset")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        // Run a workflow pipeline node-by-node against a catalog dataset.
        group.MapPost("/studio/workflows/execute/dataset", async (ExecuteFromDatasetRequest req, IKocCurrentUser me, IDatasetService datasets, IArtifactService artifacts, IPipelineExecutor executor, IConfiguration configuration, CancellationToken ct) =>
        {
            if (!Enum.TryParse<MlTaskType>(req.Task, ignoreCase: true, out var taskType) || taskType == MlTaskType.MulticlassClassification)
            {
                taskType = MlTaskType.BinaryClassification;
            }

            var access = await ResolveTrainableDatasetAsync(me, datasets, req.DatasetId, ct);
            if (access.Error is not null)
            {
                return access.Error;
            }

            try
            {
                var secondary = await ResolveSecondaryDatasetsAsync(me, datasets, artifacts, req.Definition, ct);
                await using var csv = await artifacts.OpenReadAsync(access.Dataset!.FileArtifactId!.Value, ct);
                var result = await executor.ExecuteAsync(req.Definition, req.LabelColumn, taskType, csv, maxSeconds: TrainingSeconds(configuration), secondaryDatasets: secondary, ct: ct);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = $"Execution failed: {ex.Message}" });
            }
        })
        .WithName("ExecuteWorkflowFromDataset")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        return group;
    }

    // Resolves the secondary datasets a workflow's join/union nodes reference, applying the same
    // visibility + classification gate as the primary dataset. Silently skips any the caller can't use.
    private static async Task<IReadOnlyDictionary<Guid, Stream>> ResolveSecondaryDatasetsAsync(
        IKocCurrentUser me, IDatasetService datasets, IArtifactService artifacts, WorkflowDefinition definition, CancellationToken ct)
    {
        var map = new Dictionary<Guid, Stream>();
        foreach (var id in WorkflowDatasetScanner.ReferencedDatasetIds(definition))
        {
            var access = await ResolveTrainableDatasetAsync(me, datasets, id, ct);
            if (access.Dataset?.FileArtifactId is not { } fileId)
            {
                continue;
            }

            var ms = new MemoryStream();
            await using (var s = await artifacts.OpenReadAsync(fileId, ct))
            {
                await s.CopyToAsync(ms, ct);
            }

            ms.Position = 0;
            map[id] = ms;
        }

        return map;
    }

    // Resolves a visible, file-bearing dataset the caller may train on (classification-gated like download).
    private static async Task<(Dataset? Dataset, IResult? Error)> ResolveTrainableDatasetAsync(IKocCurrentUser me, IDatasetService datasets, Guid datasetId, CancellationToken ct)
    {
        var dataset = await datasets.GetVisibleAsync(me.UserId!, datasetId, ct);
        if (dataset is null)
        {
            return (null, Results.NotFound());
        }

        if (dataset.FileArtifactId is null)
        {
            return (null, Results.BadRequest(new { error = "This dataset has no file to train on." }));
        }

        // Confidential/Restricted data requires the owner or a platform admin — same gate as download.
        if (dataset.Classification >= KocDataClassification.Confidential && !me.IsInRole(KocRoles.PlatformAdmin) && dataset.OwnerUserId != me.UserId)
        {
            return (null, Results.BadRequest(new { error = $"This dataset is classified {dataset.Classification}; training on it requires explicit permission." }));
        }

        return (dataset, null);
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static ModelRunDto ToDto(ModelRun r) =>
        new(r.Id, r.DatasetName, r.LabelColumn, r.Task, r.Algorithm, r.PrimaryMetric, r.PrimaryValue, r.SecondaryMetric, r.SecondaryValue, r.RowCount, r.CompletedUtc);
}
