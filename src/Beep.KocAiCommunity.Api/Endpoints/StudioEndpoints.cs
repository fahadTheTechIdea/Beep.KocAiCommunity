using System.Text.Json;
using Beep.KocAiCommunity.Application.ML;
using Beep.KocAiCommunity.Application.Security;
using Beep.KocAiCommunity.Application.Studio;
using Beep.KocAiCommunity.Application.Workflow;
using Beep.KocAiCommunity.Contracts.Studio;
using Beep.KocAiCommunity.Contracts.Workflow;
using Beep.KocAiCommunity.Domain.Studio;
using Microsoft.AspNetCore.Mvc;

namespace Beep.KocAiCommunity.Api.Endpoints;

public static class StudioEndpoints
{
    public static RouteGroupBuilder MapStudioEndpoints(this RouteGroupBuilder group)
    {
        // Upload a CSV and train a binary classifier via ML.NET AutoML.
        group.MapPost("/studio/train", async (IFormFile file, string labelColumn, string? datasetName, string? task, IKocCurrentUser me, IStudioService studio, CancellationToken ct) =>
        {
            if (!Enum.TryParse<MlTaskType>(task, ignoreCase: true, out var taskType))
            {
                taskType = MlTaskType.BinaryClassification;
            }

            try
            {
                await using var stream = file.OpenReadStream();
                var run = await studio.TrainAsync(me.UserId!, string.IsNullOrWhiteSpace(datasetName) ? file.FileName : datasetName, labelColumn, taskType, stream, maxSeconds: 8, ct);
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

        group.MapGet("/studio/runs", async (IKocCurrentUser me, IStudioService studio, CancellationToken ct) =>
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
        group.MapPost("/studio/workflows/execute", async (IFormFile file, [FromForm] string definition, string labelColumn, string? task, IKocCurrentUser me, IPipelineExecutor executor, CancellationToken ct) =>
        {
            if (!Enum.TryParse<MlTaskType>(task, ignoreCase: true, out var taskType) || taskType == MlTaskType.MulticlassClassification)
            {
                taskType = MlTaskType.BinaryClassification;
            }

            try
            {
                var def = JsonSerializer.Deserialize<WorkflowDefinition>(definition, JsonOptions) ?? new WorkflowDefinition();
                await using var stream = file.OpenReadStream();
                var result = await executor.ExecuteAsync(def, labelColumn, taskType, stream, maxSeconds: 8, ct);
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

        // Run a workflow: validates the graph, then trains via AutoML on the uploaded CSV.
        group.MapPost("/studio/workflows/run", async (IFormFile file, [FromForm] string definition, string labelColumn, IKocCurrentUser me, IWorkflowService workflow, CancellationToken ct) =>
        {
            try
            {
                var def = JsonSerializer.Deserialize<WorkflowDefinition>(definition, JsonOptions) ?? new WorkflowDefinition();
                await using var stream = file.OpenReadStream();
                var run = await workflow.RunAsync(me.UserId!, def, labelColumn, stream, maxSeconds: 8, ct);
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

        return group;
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static ModelRunDto ToDto(ModelRun r) =>
        new(r.Id, r.DatasetName, r.LabelColumn, r.Task, r.Algorithm, r.PrimaryMetric, r.PrimaryValue, r.SecondaryMetric, r.SecondaryValue, r.RowCount, r.CompletedUtc);
}
