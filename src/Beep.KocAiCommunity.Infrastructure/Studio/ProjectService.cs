using Beep.KocAiCommunity.Application.Studio;
using Beep.KocAiCommunity.Domain.Studio;
using Beep.KocAiCommunity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Beep.KocAiCommunity.Infrastructure.Studio;

public sealed class ProjectService(KocDbContext db) : IProjectService
{
    public async Task<Project> CreateAsync(string userId, string name, Guid? competitionId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ProjectException("A project needs a name.");
        }

        // Adopt the competition's task/label so a competition project is submit-ready out of the box.
        var task = "BinaryClassification";
        var label = "label";
        if (competitionId is { } cid)
        {
            var comp = await db.Competitions.AsNoTracking().FirstOrDefaultAsync(c => c.Id == cid, ct)
                ?? throw new ProjectException("Competition not found.");
            task = comp.TaskType;
            label = comp.LabelColumn;
        }

        var project = new Project
        {
            Name = name.Trim(),
            OwnerUserId = userId,
            CompetitionId = competitionId,
            TaskType = task,
            LabelColumn = label,
            DefinitionJson = "{}",
            CreatedByUserId = userId,
            CreatedUtc = DateTime.UtcNow,
        };
        db.Set<Project>().Add(project);
        await db.SaveChangesAsync(ct);
        return project;
    }

    public async Task<IReadOnlyList<Project>> ListMineAsync(string userId, CancellationToken ct = default) =>
        await db.Set<Project>().AsNoTracking()
            .Where(p => p.OwnerUserId == userId)
            .OrderByDescending(p => p.LastModifiedUtc ?? p.CreatedUtc)
            .ToListAsync(ct);

    public Task<Project?> GetAsync(string userId, Guid projectId, CancellationToken ct = default) =>
        db.Set<Project>().AsNoTracking().FirstOrDefaultAsync(p => p.Id == projectId && p.OwnerUserId == userId, ct);

    public async Task SaveDefinitionAsync(string userId, Guid projectId, string definitionJson, string labelColumn, string taskType, CancellationToken ct = default)
    {
        var project = await db.Set<Project>().FirstOrDefaultAsync(p => p.Id == projectId && p.OwnerUserId == userId, ct)
            ?? throw new ProjectException("Project not found.");

        project.DefinitionJson = string.IsNullOrWhiteSpace(definitionJson) ? "{}" : definitionJson;
        project.LabelColumn = string.IsNullOrWhiteSpace(labelColumn) ? project.LabelColumn : labelColumn.Trim();
        project.TaskType = string.IsNullOrWhiteSpace(taskType) ? project.TaskType : taskType.Trim();
        project.LastModifiedUtc = DateTime.UtcNow;
        project.LastModifiedByUserId = userId;
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(string userId, Guid projectId, CancellationToken ct = default)
    {
        var project = await db.Set<Project>().FirstOrDefaultAsync(p => p.Id == projectId && p.OwnerUserId == userId, ct);
        if (project is not null)
        {
            db.Set<Project>().Remove(project);
            await db.SaveChangesAsync(ct);
        }
    }
}
