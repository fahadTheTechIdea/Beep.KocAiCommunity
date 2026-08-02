using Beep.KocAiCommunity.Application.Security;
using Beep.KocAiCommunity.Application.Studio;
using Beep.KocAiCommunity.Contracts.Studio;

namespace Beep.KocAiCommunity.Platform.Endpoints;

public static class ModelEndpoints
{
    public static RouteGroupBuilder MapModelEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/models", async (RegisterModelRequest req, IKocCurrentUser me, IModelRegistry registry, CancellationToken ct) =>
        {
            try
            {
                var version = await registry.RegisterAsync(me.UserId!, req.ModelName, req.SourceRunId, ct);
                return Results.Ok(new ModelVersionDto(version.Id, version.SemVer, version.Status, version.MetricName, version.MetricValue, 0, version.RegisteredByUserId));
            }
            catch (ModelRegistryException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("RegisterModel")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        group.MapGet("/models", async (IModelRegistry registry, CancellationToken ct) =>
        {
            var models = await registry.ListAsync(ct);
            return Results.Ok(models.Select(s => new RegisteredModelDto(
                s.Model.Id, s.Model.Name, s.Model.OwnerUserId,
                [.. s.Versions.Select(v => new ModelVersionDto(v.Id, v.SemVer, v.Status, v.MetricName, v.MetricValue, s.ApprovalCounts.GetValueOrDefault(v.Id), v.RegisteredByUserId))])).ToList());
        })
        .WithName("ListModels")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        group.MapPost("/models/versions/{versionId:guid}/approve", async (Guid versionId, IKocCurrentUser me, IModelRegistry registry, CancellationToken ct) =>
        {
            try
            {
                await registry.ApproveAsync(me.UserId!, versionId, ct);
                return Results.NoContent();
            }
            catch (ModelRegistryException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("ApproveModelVersion")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        group.MapPost("/models/versions/{versionId:guid}/promote", async (Guid versionId, IKocCurrentUser me, IModelRegistry registry, CancellationToken ct) =>
        {
            try
            {
                var version = await registry.PromoteAsync(me.UserId!, versionId, ct);
                return Results.Ok(new { version.Id, version.Status });
            }
            catch (ModelRegistryException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("PromoteModelVersion")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        group.MapPost("/models/versions/{versionId:guid}/rollback", async (Guid versionId, IKocCurrentUser me, IModelRegistry registry, CancellationToken ct) =>
        {
            try
            {
                var version = await registry.RollbackAsync(me.UserId!, versionId, ct);
                return Results.Ok(new { version.Id, version.Status });
            }
            catch (ModelRegistryException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("RollbackModelVersion")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        group.MapPost("/models/versions/{versionId:guid}/deploy", async (Guid versionId, string? environment, IKocCurrentUser me, IModelRegistry registry, CancellationToken ct) =>
        {
            try
            {
                var d = await registry.DeployAsync(me.UserId!, versionId, environment ?? "production", ct);
                return Results.Ok(new { d.Id, d.Environment, d.Status });
            }
            catch (ModelRegistryException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("DeployModelVersion")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        group.MapPost("/models/deployments/{deploymentId:guid}/retire", async (Guid deploymentId, IKocCurrentUser me, IModelRegistry registry, CancellationToken ct) =>
        {
            try
            {
                await registry.RetireAsync(me.UserId!, deploymentId, ct);
                return Results.NoContent();
            }
            catch (ModelRegistryException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("RetireDeployment")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        group.MapGet("/models/deployments", async (IModelRegistry registry, CancellationToken ct) =>
        {
            var items = await registry.ListDeploymentsAsync(ct);
            return Results.Ok(items.Select(s => new DeploymentDto(
                s.Deployment.Id, s.Deployment.ModelVersionId, s.ModelName, s.SemVer, s.Deployment.Environment,
                s.Deployment.Status, s.MetricName, s.MetricValue, s.Deployment.DeployedUtc, s.Deployment.DeployedByUserId)).ToList());
        })
        .WithName("ListDeployments")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        // Online inference: score a single record.
        group.MapPost("/models/versions/{versionId:guid}/infer", async (Guid versionId, InferRequest req, IKocCurrentUser me, IInferenceService inference, CancellationToken ct) =>
        {
            try
            {
                var result = await inference.InferAsync(me.UserId!, IsAdmin(me), versionId, "online", [req.Input], ct);
                return Results.Ok(ToResponse(result));
            }
            catch (InferenceException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("InferModelVersion")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        // Batch inference: score many records.
        group.MapPost("/models/versions/{versionId:guid}/infer/batch", async (Guid versionId, BatchInferRequest req, IKocCurrentUser me, IInferenceService inference, CancellationToken ct) =>
        {
            try
            {
                var result = await inference.InferAsync(me.UserId!, IsAdmin(me), versionId, "batch", req.Rows, ct);
                return Results.Ok(ToResponse(result));
            }
            catch (InferenceException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("InferModelVersionBatch")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        // Inference audit log (owner/admin only).
        group.MapGet("/models/versions/{versionId:guid}/inference-logs", async (Guid versionId, int? take, IKocCurrentUser me, IInferenceService inference, CancellationToken ct) =>
        {
            try
            {
                var logs = await inference.GetLogsAsync(me.UserId!, IsAdmin(me), versionId, take ?? 50, ct);
                return Results.Ok(logs.Select(l => new InferenceLogDto(
                    l.Id, l.CallerUserId, l.Endpoint, l.RowCount, l.LatencyMs, l.CalledUtc, l.Success, l.Error)).ToList());
            }
            catch (InferenceException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("ModelInferenceLogs")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        // Drift check: compare a batch's feature means to the training baseline.
        group.MapPost("/models/versions/{versionId:guid}/drift", async (Guid versionId, DriftRequest req, IKocCurrentUser me, IInferenceService inference, CancellationToken ct) =>
        {
            try
            {
                var report = await inference.ComputeDriftAsync(me.UserId!, IsAdmin(me), versionId, req.Rows, ct);
                return Results.Ok(new DriftReportDto(report.BaselineRows, report.BatchRows,
                    report.Features.Select(f => new FeatureDriftDto(f.Feature, f.BaselineMean, f.BatchMean, f.MeanShift, f.Drifted)).ToList(),
                    report.AnyDrift));
            }
            catch (InferenceException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("ModelVersionDrift")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        return group;
    }

    private static bool IsAdmin(IKocCurrentUser me) => me.IsInRole(KocRoles.PlatformAdmin);

    private static InferResponseDto ToResponse(Application.ML.InferenceResult result) =>
        new(result.Predictions.Select(p => new PredictionDto(p.PredictedLabel, p.Probability, p.Score, p.Scores)).ToList());
}
