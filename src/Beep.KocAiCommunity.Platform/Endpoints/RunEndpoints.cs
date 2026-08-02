using System.Text.Json;
using Beep.KocAiCommunity.Application.Jobs;
using Beep.KocAiCommunity.Application.Security;
using Beep.KocAiCommunity.Contracts.Jobs;
using Beep.KocAiCommunity.Domain.Jobs;

namespace Beep.KocAiCommunity.Platform.Endpoints;

public static class RunEndpoints
{
    public static RouteGroupBuilder MapRunEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/runs", async (CreateRunRequest req, IKocCurrentUser me, IJobQueue queue, CancellationToken ct) =>
        {
            var payload = StampOwner(req.Type, req.PayloadJson, me.UserId!);
            var id = await queue.EnqueueAsync(req.Type, req.Title, payload, me.UserId!, req.Priority, maxAttempts: 3, ct);
            var job = await queue.GetAsync(id, ct);
            return Results.Ok(ToDto(job!));
        })
        .WithName("CreateRun")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        group.MapGet("/runs", async (IKocCurrentUser me, IJobQueue queue, CancellationToken ct) =>
        {
            var runs = await queue.GetRecentForOwnerAsync(me.UserId!, 30, ct);
            return Results.Ok(runs.Select(ToDto).ToList());
        })
        .WithName("GetMyRuns")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        group.MapGet("/runs/{id:guid}", async (Guid id, IKocCurrentUser me, IJobQueue queue, CancellationToken ct) =>
        {
            var job = await queue.GetAsync(id, ct);
            return job is null || !CanSee(job, me) ? Results.NotFound() : Results.Ok(ToDto(job));
        })
        .WithName("GetRun")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        group.MapPost("/runs/{id:guid}/cancel", async (Guid id, IKocCurrentUser me, IJobQueue queue, CancellationToken ct) =>
        {
            var job = await queue.GetAsync(id, ct);
            if (job is null || !CanSee(job, me))
            {
                return Results.NotFound();
            }

            await queue.RequestCancelAsync(id, ct);
            return Results.NoContent();
        })
        .WithName("CancelRun")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        group.MapGet("/runs/{id:guid}/logs", async (Guid id, IKocCurrentUser me, IJobQueue queue, CancellationToken ct) =>
        {
            var job = await queue.GetAsync(id, ct);
            if (job is null || !CanSee(job, me))
            {
                return Results.NotFound();
            }

            var logs = await queue.GetLogsAsync(id, ct);
            return Results.Ok(logs.Select(l => new RunLogDto(l.LoggedUtc, l.Severity, l.Message)).ToList());
        })
        .WithName("GetRunLogs")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        group.MapGet("/runs/{id:guid}/attempts", async (Guid id, IKocCurrentUser me, IJobQueue queue, CancellationToken ct) =>
        {
            var job = await queue.GetAsync(id, ct);
            if (job is null || !CanSee(job, me))
            {
                return Results.NotFound();
            }

            var attempts = await queue.GetAttemptsAsync(id, ct);
            return Results.Ok(attempts
                .Select(a => new RunAttemptDto(a.AttemptNumber, a.Status, a.StartedUtc, a.CompletedUtc, a.WorkerId, a.Error))
                .ToList());
        })
        .WithName("GetRunAttempts")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        return group;
    }

    // Owner or PlatformAdmin may see/cancel a run.
    private static bool CanSee(Job job, IKocCurrentUser me) =>
        job.OwnerUserId == me.UserId || me.IsInRole(KocRoles.PlatformAdmin);

    // Never let a client run a training job as another user: force the payload owner to the caller.
    private static string StampOwner(string type, string payloadJson, string userId)
    {
        if (type != JobTypes.ModelTrain)
        {
            return string.IsNullOrWhiteSpace(payloadJson) ? "{}" : payloadJson;
        }

        try
        {
            var payload = JsonSerializer.Deserialize<ModelTrainPayload>(payloadJson);
            if (payload is null)
            {
                return payloadJson;
            }

            return JsonSerializer.Serialize(payload with { OwnerUserId = userId });
        }
        catch (JsonException)
        {
            return payloadJson;
        }
    }

    private static RunDto ToDto(Job j) => new(
        j.Id, j.Type, j.Title, j.Status, j.AttemptCount, j.MaxAttempts, j.Priority,
        j.CreatedUtc, j.StartedUtc, j.CompletedUtc, j.LastError);
}
