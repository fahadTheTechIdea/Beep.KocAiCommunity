using Beep.KocAiCommunity.Application.Security;
using Beep.KocAiCommunity.Application.Studio;
using Beep.KocAiCommunity.Application.Workflow;
using Beep.KocAiCommunity.Contracts.Studio;
using Beep.KocAiCommunity.Contracts.Workflow;
using Beep.KocAiCommunity.Domain.Studio;

namespace Beep.KocAiCommunity.Platform.Endpoints;

/// <summary>
/// What is left of Studio on the server.
/// <para>
/// <b>The website does not train.</b> Building and training a model happens in KOC Studio on the
/// desktop, against that machine's own cores; the platform's job is to keep the record of what was
/// trained and to serve the competitions people train for. The endpoints that used to run AutoML here
/// — <c>/studio/train</c>, <c>/studio/train/dataset</c>, <c>/studio/workflows/execute</c>,
/// <c>/workflows/run</c> and <c>/workflows/execute/dataset</c> — were left behind when Studio moved to
/// the desktop, and are gone.
/// </para>
/// <para>
/// What remains reads or checks and never executes: the run history, and graph validation, which is a
/// structural check on the definition and runs no nodes.
/// </para>
/// </summary>
public static class StudioEndpoints
{
    public static RouteGroupBuilder MapStudioEndpoints(this RouteGroupBuilder group)
    {
        // Runs recorded by the desktop. Reading the history, not making it.
        group.MapGet("/studio/runs", async (IKocCurrentUser me, IStudioService studio, CancellationToken ct) =>
        {
            var runs = await studio.GetMyRunsAsync(me.UserId!, ct);
            return Results.Ok(runs.Select(ToDto).ToList());
        })
        .WithName("StudioRuns")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        // A structural check on a graph — source and train present, acyclic. It reads the definition and
        // runs nothing, so it stays: telling somebody their graph is malformed does not require
        // executing it.
        group.MapPost("/studio/workflows/validate", (WorkflowDefinition definition, IWorkflowService workflow) =>
            Results.Ok(workflow.Validate(definition)))
        .WithName("ValidateWorkflow")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        return group;
    }

    private static ModelRunDto ToDto(ModelRun r) =>
        new(r.Id, r.DatasetName, r.LabelColumn, r.Task, r.Algorithm, r.PrimaryMetric, r.PrimaryValue,
            r.SecondaryMetric, r.SecondaryValue, r.RowCount, r.CompletedUtc);
}
