using Beep.KocAiCommunity.Application.Workflow;
using Beep.KocAiCommunity.Domain.Common;
using Beep.KocAiCommunity.Domain.Studio;
using Beep.KocAiCommunity.Infrastructure.Persistence;
using Beep.KocAiCommunity.Workflow;
using Microsoft.EntityFrameworkCore;
using WorkflowEntity = Beep.KocAiCommunity.Domain.Studio.Workflow;

namespace Beep.KocAiCommunity.Infrastructure.Workflow;

/// <summary>Lists seeded workflow templates and instantiates one into a new, owned draft workflow.</summary>
public sealed class WorkflowTemplateService(KocDbContext db) : IWorkflowTemplateService
{
    public async Task<IReadOnlyList<WorkflowTemplate>> ListAsync(string? domain, CancellationToken ct = default)
    {
        var q = db.Set<WorkflowTemplate>().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(domain))
        {
            q = q.Where(t => t.Domain == domain);
        }

        return await q.OrderBy(t => t.DisplayName).ToListAsync(ct);
    }

    public async Task<WorkflowEntity> InstantiateAsync(string userId, string code, string name, CancellationToken ct = default)
    {
        var template = await db.Set<WorkflowTemplate>().AsNoTracking().FirstOrDefaultAsync(t => t.Code == code, ct)
            ?? throw new WorkflowRegistryException("Template not found.");

        // Re-canonicalize the template so the new draft's hash is computed the same way as any other.
        var canonical = WorkflowSerializer.Canonicalize(template.DefinitionJson);
        var workflow = new WorkflowEntity
        {
            Name = string.IsNullOrWhiteSpace(name) ? template.DisplayName : name.Trim(),
            Description = template.Description,
            OwnerUserId = userId,
            Classification = KocDataClassification.Internal,
            LatestVersionNumber = 1,
            CreatedByUserId = userId,
            CreatedUtc = DateTime.UtcNow,
        };
        db.Set<WorkflowEntity>().Add(workflow);

        db.Set<WorkflowVersion>().Add(new WorkflowVersion
        {
            WorkflowId = workflow.Id,
            VersionNumber = 1,
            Status = "draft",
            SchemaVersion = template.SchemaVersion,
            DefinitionJson = canonical,
            SnapshotHash = WorkflowSerializer.Hash(canonical),
            Notes = $"From template '{template.Code}'",
            CreatedByUserId = userId,
            CreatedUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(ct);
        return workflow;
    }
}
