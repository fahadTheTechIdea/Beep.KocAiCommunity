using Beep.KocAiCommunity.Application.Security;
using Beep.KocAiCommunity.Application.Workflow;
using Beep.KocAiCommunity.Contracts.Workflow;
using Beep.KocAiCommunity.Domain.Common;
using Beep.KocAiCommunity.Domain.Studio;
using WorkflowEntity = Beep.KocAiCommunity.Domain.Studio.Workflow;

namespace Beep.KocAiCommunity.Api.Endpoints;

/// <summary>Versioned workflow registry: workflows, immutable versions, import/export, and templates.</summary>
public static class WorkflowRegistryEndpoints
{
    public static RouteGroupBuilder MapWorkflowRegistryEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/workflows", async (CreateWorkflowRequest req, IKocCurrentUser me, IWorkflowVersionService svc, CancellationToken ct) =>
        {
            if (!Enum.TryParse<KocDataClassification>(req.Classification, ignoreCase: true, out var classification))
            {
                classification = KocDataClassification.Internal;
            }

            try
            {
                var wf = await svc.CreateAsync(me.UserId!, req.Name, req.Description ?? "", classification, ct);
                return Results.Ok(ToSummary(wf, 1, "draft"));
            }
            catch (WorkflowRegistryException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).WithName("CreateWorkflow").RequireAuthorization(KocPolicies.RequireEmployee);

        group.MapGet("/workflows", async (IKocCurrentUser me, IWorkflowVersionService svc, CancellationToken ct) =>
        {
            var items = await svc.ListAsync(me.UserId!, ct);
            return Results.Ok(items.Select(s => ToSummary(s.Workflow, s.VersionCount, s.LatestStatus)).ToList());
        }).WithName("ListWorkflows").RequireAuthorization(KocPolicies.RequireEmployee);

        group.MapGet("/workflows/{id:guid}", async (Guid id, IKocCurrentUser me, IWorkflowVersionService svc, CancellationToken ct) =>
        {
            var detail = await svc.GetAsync(me.UserId!, id, ct);
            return detail is null
                ? Results.NotFound()
                : Results.Ok(new WorkflowDetailDto(
                    detail.Workflow.Id, detail.Workflow.Name, detail.Workflow.Description, detail.Workflow.OwnerUserId,
                    detail.Workflow.Classification.ToString(), detail.Workflow.LatestVersionNumber,
                    [.. detail.Versions.Select(ToVersionDto)]));
        }).WithName("GetWorkflow").RequireAuthorization(KocPolicies.RequireEmployee);

        group.MapDelete("/workflows/{id:guid}", (Guid id, IKocCurrentUser me, IWorkflowVersionService svc, CancellationToken ct) =>
            Guard(() => svc.DeleteAsync(me.UserId!, IsAdmin(me), id, ct)))
        .WithName("DeleteWorkflow").RequireAuthorization(KocPolicies.RequireEmployee);

        group.MapGet("/workflows/{id:guid}/versions/{n:int}", async (Guid id, int n, IKocCurrentUser me, IWorkflowVersionService svc, CancellationToken ct) =>
        {
            var v = await svc.GetVersionAsync(me.UserId!, id, n, ct);
            return v is null
                ? Results.NotFound()
                : Results.Ok(new WorkflowVersionDetailDto(v.VersionNumber, v.Status, v.SchemaVersion, v.DefinitionJson, v.SnapshotHash, v.Notes));
        }).WithName("GetWorkflowVersion").RequireAuthorization(KocPolicies.RequireEmployee);

        group.MapPost("/workflows/{id:guid}/versions", async (Guid id, SaveDraftRequest req, IKocCurrentUser me, IWorkflowVersionService svc, CancellationToken ct) =>
        {
            try
            {
                var v = await svc.SaveDraftAsync(me.UserId!, IsAdmin(me), id, req.DefinitionJson, req.Notes, ct);
                return Results.Ok(ToVersionDto(v));
            }
            catch (WorkflowRegistryException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).WithName("SaveWorkflowDraft").RequireAuthorization(KocPolicies.RequireEmployee);

        group.MapPost("/workflows/{id:guid}/versions/{n:int}/publish", async (Guid id, int n, IKocCurrentUser me, IWorkflowVersionService svc, CancellationToken ct) =>
        {
            try
            {
                var v = await svc.PublishAsync(me.UserId!, IsAdmin(me), id, n, ct);
                return Results.Ok(ToVersionDto(v));
            }
            catch (WorkflowRegistryException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).WithName("PublishWorkflowVersion").RequireAuthorization(KocPolicies.RequireEmployee);

        group.MapPost("/workflows/{id:guid}/versions/{n:int}/archive", async (Guid id, int n, IKocCurrentUser me, IWorkflowVersionService svc, CancellationToken ct) =>
        {
            try
            {
                var v = await svc.ArchiveAsync(me.UserId!, IsAdmin(me), id, n, ct);
                return Results.Ok(ToVersionDto(v));
            }
            catch (WorkflowRegistryException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).WithName("ArchiveWorkflowVersion").RequireAuthorization(KocPolicies.RequireEmployee);

        group.MapPost("/workflows/{id:guid}/versions/{n:int}/validate", async (Guid id, int n, IKocCurrentUser me, IWorkflowVersionService svc, CancellationToken ct) =>
        {
            try
            {
                return Results.Ok(await svc.ValidateAsync(me.UserId!, id, n, ct));
            }
            catch (WorkflowRegistryException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).WithName("ValidateWorkflowVersion").RequireAuthorization(KocPolicies.RequireEmployee);

        group.MapGet("/workflows/{id:guid}/versions/{n:int}/export", async (Guid id, int n, IKocCurrentUser me, IWorkflowVersionService svc, CancellationToken ct) =>
        {
            try
            {
                var envelope = await svc.ExportAsync(me.UserId!, id, n, ct);
                return Results.Ok(new WorkflowExportDto(envelope));
            }
            catch (WorkflowRegistryException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).WithName("ExportWorkflowVersion").RequireAuthorization(KocPolicies.RequireEmployee);

        group.MapPost("/workflows/import", async (ImportWorkflowRequest req, IKocCurrentUser me, IWorkflowVersionService svc, CancellationToken ct) =>
        {
            try
            {
                var wf = await svc.ImportAsync(me.UserId!, req.Name, req.EnvelopeJson, ct);
                return Results.Ok(ToSummary(wf, 1, "draft"));
            }
            catch (WorkflowRegistryException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).WithName("ImportWorkflow").RequireAuthorization(KocPolicies.RequireEmployee);

        group.MapGet("/workflow-templates", async (string? domain, IWorkflowTemplateService svc, CancellationToken ct) =>
        {
            var items = await svc.ListAsync(domain, ct);
            return Results.Ok(items.Select(t => new WorkflowTemplateDto(t.Code, t.DisplayName, t.Description, t.Domain, t.SchemaVersion)).ToList());
        }).WithName("ListWorkflowTemplates").RequireAuthorization(KocPolicies.RequireEmployee);

        group.MapPost("/workflow-templates/{code}/instantiate", async (string code, InstantiateTemplateRequest req, IKocCurrentUser me, IWorkflowTemplateService svc, CancellationToken ct) =>
        {
            try
            {
                var wf = await svc.InstantiateAsync(me.UserId!, code, req.Name, ct);
                return Results.Ok(ToSummary(wf, 1, "draft"));
            }
            catch (WorkflowRegistryException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).WithName("InstantiateWorkflowTemplate").RequireAuthorization(KocPolicies.RequireEmployee);

        return group;
    }

    private static bool IsAdmin(IKocCurrentUser me) => me.IsInRole(KocRoles.PlatformAdmin);

    private static async Task<IResult> Guard(Func<Task> action)
    {
        try
        {
            await action();
            return Results.NoContent();
        }
        catch (WorkflowRegistryException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static WorkflowSummaryDto ToSummary(WorkflowEntity w, int versionCount, string latestStatus) =>
        new(w.Id, w.Name, w.Description, w.OwnerUserId, w.Classification.ToString(), w.LatestVersionNumber, versionCount, latestStatus, w.CreatedUtc);

    private static WorkflowVersionDto ToVersionDto(WorkflowVersion v) =>
        new(v.VersionNumber, v.Status, v.SchemaVersion, v.SnapshotHash, v.Notes, v.PublishedUtc, v.CreatedUtc);
}
