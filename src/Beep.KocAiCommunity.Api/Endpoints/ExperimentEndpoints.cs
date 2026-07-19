using Beep.KocAiCommunity.Application.Experiments;
using Beep.KocAiCommunity.Application.Security;
using Beep.KocAiCommunity.Application.Studio;
using Beep.KocAiCommunity.Contracts.Experiments;
using Beep.KocAiCommunity.Contracts.Studio;

namespace Beep.KocAiCommunity.Api.Endpoints;

public static class ExperimentEndpoints
{
    public static RouteGroupBuilder MapExperimentEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/experiments", async (CreateExperimentRequest req, IKocCurrentUser me, IExperimentService svc, CancellationToken ct) =>
            Results.Ok(await svc.CreateAsync(me.UserId!, req, ct)))
        .WithName("CreateExperiment")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        group.MapGet("/experiments", async (IKocCurrentUser me, IExperimentService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListForOwnerAsync(me.UserId!, ct)))
        .WithName("ListExperiments")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        group.MapGet("/experiments/{id:guid}", async (Guid id, IExperimentService svc, CancellationToken ct) =>
        {
            var experiment = await svc.GetAsync(id, ct);
            return experiment is null ? Results.NotFound() : Results.Ok(experiment);
        })
        .WithName("GetExperiment")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        group.MapGet("/experiments/{id:guid}/runs", async (Guid id, IExperimentService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListRunsAsync(id, ct)))
        .WithName("ListExperimentRuns")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        group.MapGet("/experiments/{id:guid}/compare", async (Guid id, IExperimentService svc, CancellationToken ct) =>
            Results.Ok(await svc.CompareAsync(id, ct)))
        .WithName("CompareExperiment")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        group.MapGet("/experiments/{id:guid}/best-run", async (Guid id, IExperimentService svc, CancellationToken ct) =>
        {
            var best = await svc.GetBestRunAsync(id, ct);
            return best is null ? Results.NotFound() : Results.Ok(best);
        })
        .WithName("GetBestRun")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        // Runs are namespaced under /experiments/runs so they don't collide with the Phase 10 job /runs.
        group.MapGet("/experiments/runs/{runId:guid}", async (Guid runId, IExperimentService svc, CancellationToken ct) =>
        {
            var run = await svc.GetRunAsync(runId, ct);
            return run is null ? Results.NotFound() : Results.Ok(run);
        })
        .WithName("GetExperimentRun")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        group.MapPut("/experiments/runs/{runId:guid}", async (Guid runId, UpdateRunRequest req, IKocCurrentUser me, IExperimentService svc, CancellationToken ct) =>
        {
            var run = await svc.GetRunAsync(runId, ct);
            if (run is null || (run.RunByUserId != me.UserId && !me.IsInRole(KocRoles.PlatformAdmin)))
            {
                return Results.NotFound();
            }

            var updated = await svc.UpdateRunAsync(runId, req, ct);
            return Results.Ok(updated);
        })
        .WithName("UpdateExperimentRun")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        group.MapGet("/experiments/runs/{runId:guid}/metrics", async (Guid runId, IExperimentService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetMetricsAsync(runId, ct)))
        .WithName("GetRunMetrics")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        group.MapPost("/experiments/runs/{runId:guid}/metrics", async (Guid runId, LogMetricsRequest req, IExperimentService svc, CancellationToken ct) =>
        {
            var entries = req.Metrics.Select(m => new RunMetricEntry(m.Name, m.Value, m.Dataset, m.Phase, m.Step)).ToList();
            await svc.LogMetricsAsync(runId, entries, ct);
            return Results.NoContent();
        })
        .WithName("LogRunMetrics")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        // Register a run's produced model into the model registry (track → register).
        group.MapPost("/experiments/runs/{runId:guid}/register", async (Guid runId, RegisterRunRequest req, IKocCurrentUser me, IExperimentService svc, IModelRegistry registry, CancellationToken ct) =>
        {
            var run = await svc.GetRunAsync(runId, ct);
            if (run is null)
            {
                return Results.NotFound();
            }

            if (run.ModelRunId is null)
            {
                return Results.BadRequest(new { error = "This run has no saved model to register." });
            }

            if (run.RunByUserId != me.UserId && !me.IsInRole(KocRoles.PlatformAdmin))
            {
                return Results.BadRequest(new { error = "Only the run owner or a platform admin can register it." });
            }

            try
            {
                var version = await registry.RegisterAsync(me.UserId!, req.ModelName, run.ModelRunId.Value, ct);
                return Results.Ok(new ModelVersionDto(version.Id, version.SemVer, version.Status, version.MetricName, version.MetricValue, 0, version.RegisteredByUserId));
            }
            catch (ModelRegistryException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("RegisterExperimentRun")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        group.MapGet("/experiments/runs/{runId:guid}/parameters", async (Guid runId, IExperimentService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetParametersAsync(runId, ct)))
        .WithName("GetRunParameters")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        return group;
    }
}
