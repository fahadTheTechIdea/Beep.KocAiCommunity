using Beep.KocAiCommunity.Application.Competitions;
using Beep.KocAiCommunity.Application.Security;
using Beep.KocAiCommunity.Application.Studio;
using Beep.KocAiCommunity.Contracts.Studio;
using Beep.KocAiCommunity.Domain.Studio;

namespace Beep.KocAiCommunity.Api.Endpoints;

public static class ProjectEndpoints
{
    public static RouteGroupBuilder MapProjectEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/projects", async (IKocCurrentUser me, IProjectService svc, ICompetitionService comps, CancellationToken ct) =>
        {
            var projects = await svc.ListMineAsync(me.UserId!, ct);
            var titles = await ResolveTitlesAsync(projects, comps, ct);
            return Results.Ok(projects.Select(p => ToDto(p, titles)).ToList());
        })
        .WithName("ListMyProjects")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        group.MapPost("/projects", async (CreateProjectRequest req, IKocCurrentUser me, IProjectService svc, ICompetitionService comps, CancellationToken ct) =>
        {
            try
            {
                var project = await svc.CreateAsync(me.UserId!, req.Name, req.CompetitionId, ct);
                var titles = await ResolveTitlesAsync([project], comps, ct);
                return Results.Ok(ToDto(project, titles));
            }
            catch (ProjectException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("CreateProject")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        group.MapGet("/projects/{id:guid}", async (Guid id, IKocCurrentUser me, IProjectService svc, ICompetitionService comps, CancellationToken ct) =>
        {
            var project = await svc.GetAsync(me.UserId!, id, ct);
            if (project is null)
            {
                return Results.NotFound();
            }

            var title = project.CompetitionId is { } cid ? (await comps.GetAsync(cid, ct))?.Title : null;
            return Results.Ok(new ProjectDetailDto(project.Id, project.Name, project.CompetitionId, title, project.TaskType, project.LabelColumn, project.DefinitionJson));
        })
        .WithName("GetProject")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        group.MapPut("/projects/{id:guid}", async (Guid id, SaveProjectRequest req, IKocCurrentUser me, IProjectService svc, CancellationToken ct) =>
        {
            try
            {
                await svc.SaveDefinitionAsync(me.UserId!, id, req.DefinitionJson, req.LabelColumn, req.TaskType, ct);
                return Results.NoContent();
            }
            catch (ProjectException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("SaveProject")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        group.MapDelete("/projects/{id:guid}", async (Guid id, IKocCurrentUser me, IProjectService svc, CancellationToken ct) =>
        {
            await svc.DeleteAsync(me.UserId!, id, ct);
            return Results.NoContent();
        })
        .WithName("DeleteProject")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        return group;
    }

    private static async Task<Dictionary<Guid, string>> ResolveTitlesAsync(IReadOnlyList<Project> projects, ICompetitionService comps, CancellationToken ct)
    {
        var titles = new Dictionary<Guid, string>();
        foreach (var cid in projects.Where(p => p.CompetitionId is not null).Select(p => p.CompetitionId!.Value).Distinct())
        {
            var comp = await comps.GetAsync(cid, ct);
            if (comp is not null)
            {
                titles[cid] = comp.Title;
            }
        }
        return titles;
    }

    private static ProjectDto ToDto(Project p, Dictionary<Guid, string> titles) =>
        new(p.Id, p.Name, p.CompetitionId,
            p.CompetitionId is { } cid && titles.TryGetValue(cid, out var t) ? t : null,
            p.TaskType, p.LabelColumn, p.LastModifiedUtc ?? p.CreatedUtc);
}
