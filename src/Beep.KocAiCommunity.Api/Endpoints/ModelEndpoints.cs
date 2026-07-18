using Beep.KocAiCommunity.Application.Security;
using Beep.KocAiCommunity.Application.Studio;
using Beep.KocAiCommunity.Contracts.Studio;

namespace Beep.KocAiCommunity.Api.Endpoints;

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

        return group;
    }
}
