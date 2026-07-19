using System.Text.Json;
using Beep.KocAiCommunity.Application.Workflow;
using Beep.KocAiCommunity.Contracts.Workflow;
using Beep.KocAiCommunity.Domain.Common;
using Beep.KocAiCommunity.Domain.Studio;
using Beep.KocAiCommunity.Infrastructure.Persistence;
using Beep.KocAiCommunity.Workflow;
using Microsoft.EntityFrameworkCore;
using WorkflowEntity = Beep.KocAiCommunity.Domain.Studio.Workflow;

namespace Beep.KocAiCommunity.Infrastructure.Workflow;

/// <summary>
/// Manages workflows and their immutable versions. Drafts are editable in place; publishing validates
/// the graph with <see cref="WorkflowCompiler"/> and then freezes the version. Definitions are stored
/// canonicalized so the snapshot hash is stable and round-trips without loss.
/// </summary>
public sealed class WorkflowVersionService(KocDbContext db) : IWorkflowVersionService
{
    private const string ExportKind = "koc-workflow-export";

    public async Task<WorkflowEntity> CreateAsync(string userId, string name, string description, KocDataClassification classification, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new WorkflowRegistryException("A workflow name is required.");
        }

        var workflow = new WorkflowEntity
        {
            Name = name.Trim(),
            Description = description ?? string.Empty,
            OwnerUserId = userId,
            Classification = classification,
            LatestVersionNumber = 1,
            CreatedByUserId = userId,
            CreatedUtc = DateTime.UtcNow,
        };
        db.Set<WorkflowEntity>().Add(workflow);

        var seed = WorkflowSerializer.Canonicalize(new WorkflowDefinition { Name = workflow.Name });
        db.Set<WorkflowVersion>().Add(NewDraft(workflow.Id, 1, seed, "Initial draft", userId));
        await db.SaveChangesAsync(ct);
        return workflow;
    }

    public async Task<IReadOnlyList<WorkflowSummary>> ListAsync(string userId, CancellationToken ct = default)
    {
        var workflows = await db.Set<WorkflowEntity>().AsNoTracking()
            .Where(w => w.OwnerUserId == userId && !w.IsDeleted)
            .OrderByDescending(w => w.LastModifiedUtc ?? w.CreatedUtc)
            .ToListAsync(ct);

        var ids = workflows.Select(w => w.Id).ToList();
        var versions = await db.Set<WorkflowVersion>().AsNoTracking()
            .Where(v => ids.Contains(v.WorkflowId))
            .Select(v => new { v.WorkflowId, v.VersionNumber, v.Status })
            .ToListAsync(ct);

        return workflows.Select(w =>
        {
            var vs = versions.Where(v => v.WorkflowId == w.Id).ToList();
            var latest = vs.OrderByDescending(v => v.VersionNumber).FirstOrDefault();
            return new WorkflowSummary(w, vs.Count, latest?.Status ?? "draft");
        }).ToList();
    }

    public async Task<WorkflowDetail?> GetAsync(string userId, Guid workflowId, CancellationToken ct = default)
    {
        var workflow = await db.Set<WorkflowEntity>().AsNoTracking().FirstOrDefaultAsync(w => w.Id == workflowId && !w.IsDeleted, ct);
        if (workflow is null)
        {
            return null;
        }

        var versions = await db.Set<WorkflowVersion>().AsNoTracking()
            .Where(v => v.WorkflowId == workflowId)
            .OrderByDescending(v => v.VersionNumber)
            .ToListAsync(ct);
        return new WorkflowDetail(workflow, versions);
    }

    public async Task<WorkflowVersion?> GetVersionAsync(string userId, Guid workflowId, int versionNumber, CancellationToken ct = default) =>
        await db.Set<WorkflowVersion>().AsNoTracking()
            .FirstOrDefaultAsync(v => v.WorkflowId == workflowId && v.VersionNumber == versionNumber, ct);

    public async Task<WorkflowVersion> SaveDraftAsync(string userId, bool isAdmin, Guid workflowId, string definitionJson, string? notes, CancellationToken ct = default)
    {
        var workflow = await RequireEditableAsync(userId, isAdmin, workflowId, ct);
        var canonical = Canonicalize(definitionJson);
        var hash = WorkflowSerializer.Hash(definitionJson);

        var latest = await db.Set<WorkflowVersion>()
            .Where(v => v.WorkflowId == workflowId)
            .OrderByDescending(v => v.VersionNumber)
            .FirstOrDefaultAsync(ct);

        WorkflowVersion draft;
        if (latest is { Status: "draft" })
        {
            // Edit the open draft in place — published versions are never touched.
            latest.DefinitionJson = canonical;
            latest.SnapshotHash = hash;
            latest.Notes = notes ?? latest.Notes;
            latest.LastModifiedByUserId = userId;
            latest.LastModifiedUtc = DateTime.UtcNow;
            draft = latest;
        }
        else
        {
            var next = workflow.LatestVersionNumber + 1;
            draft = NewDraft(workflowId, next, canonical, notes, userId);
            draft.SnapshotHash = hash;
            db.Set<WorkflowVersion>().Add(draft);
            workflow.LatestVersionNumber = next;
        }

        workflow.LastModifiedByUserId = userId;
        workflow.LastModifiedUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return draft;
    }

    public async Task<WorkflowVersion> PublishAsync(string userId, bool isAdmin, Guid workflowId, int versionNumber, CancellationToken ct = default)
    {
        await RequireEditableAsync(userId, isAdmin, workflowId, ct);
        var version = await RequireVersionAsync(workflowId, versionNumber, ct);
        if (version.Status != "draft")
        {
            throw new WorkflowRegistryException("Only a draft version can be published.");
        }

        var definition = WorkflowSerializer.Parse(version.DefinitionJson);
        var compiled = WorkflowCompiler.Compile(definition);
        if (!compiled.IsValid)
        {
            throw new WorkflowRegistryException($"Can't publish — the workflow doesn't compile: {string.Join(" ", compiled.Errors)}");
        }

        // Split-before-fit: a published workflow must not fit a supervised model on the full dataset.
        var leaks = Application.ML.FeaturizationGuard.Check(definition);
        if (leaks.Count > 0)
        {
            throw new WorkflowRegistryException($"Can't publish — {string.Join(" ", leaks)}");
        }

        version.Status = "published";
        version.PublishedUtc = DateTime.UtcNow;
        version.PublishedByUserId = userId;
        version.LastModifiedByUserId = userId;
        version.LastModifiedUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return version;
    }

    public async Task<WorkflowVersion> ArchiveAsync(string userId, bool isAdmin, Guid workflowId, int versionNumber, CancellationToken ct = default)
    {
        await RequireEditableAsync(userId, isAdmin, workflowId, ct);
        var version = await RequireVersionAsync(workflowId, versionNumber, ct);
        if (version.Status != "published")
        {
            throw new WorkflowRegistryException("Only a published version can be archived.");
        }

        version.Status = "archived";
        version.LastModifiedByUserId = userId;
        version.LastModifiedUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return version;
    }

    public async Task DeleteAsync(string userId, bool isAdmin, Guid workflowId, CancellationToken ct = default)
    {
        var workflow = await RequireEditableAsync(userId, isAdmin, workflowId, ct);
        workflow.IsDeleted = true;
        workflow.LastModifiedByUserId = userId;
        workflow.LastModifiedUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task<WorkflowValidationResult> ValidateAsync(string userId, Guid workflowId, int versionNumber, CancellationToken ct = default)
    {
        var version = await GetVersionAsync(userId, workflowId, versionNumber, ct)
            ?? throw new WorkflowRegistryException("Workflow version not found.");
        return WorkflowCompiler.Compile(WorkflowSerializer.Parse(version.DefinitionJson));
    }

    public async Task<string> ExportAsync(string userId, Guid workflowId, int versionNumber, CancellationToken ct = default)
    {
        var workflow = await db.Set<WorkflowEntity>().AsNoTracking().FirstOrDefaultAsync(w => w.Id == workflowId && !w.IsDeleted, ct)
            ?? throw new WorkflowRegistryException("Workflow not found.");
        var version = await RequireVersionAsync(workflowId, versionNumber, ct);

        var export = new WorkflowExport(ExportKind, version.SchemaVersion, workflow.Name,
            WorkflowSerializer.Parse(version.DefinitionJson), version.SnapshotHash);
        return JsonSerializer.Serialize(export, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true });
    }

    public async Task<WorkflowEntity> ImportAsync(string userId, string name, string envelopeJson, CancellationToken ct = default)
    {
        WorkflowExport? export;
        try
        {
            export = JsonSerializer.Deserialize<WorkflowExport>(envelopeJson, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        }
        catch (JsonException ex)
        {
            throw new WorkflowRegistryException($"The import file is not valid JSON: {ex.Message}");
        }

        if (export?.Definition is null)
        {
            throw new WorkflowRegistryException("The import file has no workflow definition.");
        }

        var canonical = WorkflowSerializer.Canonicalize(export.Definition);
        var chosenName = string.IsNullOrWhiteSpace(name) ? (string.IsNullOrWhiteSpace(export.Name) ? "Imported workflow" : export.Name) : name;

        var workflow = new WorkflowEntity
        {
            Name = chosenName.Trim(),
            Description = string.Empty,
            OwnerUserId = userId,
            Classification = KocDataClassification.Internal,
            LatestVersionNumber = 1,
            CreatedByUserId = userId,
            CreatedUtc = DateTime.UtcNow,
        };
        db.Set<WorkflowEntity>().Add(workflow);

        var draft = NewDraft(workflow.Id, 1, canonical, "Imported", userId);
        draft.SnapshotHash = WorkflowSerializer.Hash(canonical);
        db.Set<WorkflowVersion>().Add(draft);
        await db.SaveChangesAsync(ct);
        return workflow;
    }

    private async Task<WorkflowEntity> RequireEditableAsync(string userId, bool isAdmin, Guid workflowId, CancellationToken ct)
    {
        var workflow = await db.Set<WorkflowEntity>().FirstOrDefaultAsync(w => w.Id == workflowId && !w.IsDeleted, ct)
            ?? throw new WorkflowRegistryException("Workflow not found.");
        if (!isAdmin && workflow.OwnerUserId != userId)
        {
            throw new WorkflowRegistryException("Only the workflow owner or a platform admin can change it.");
        }

        return workflow;
    }

    private async Task<WorkflowVersion> RequireVersionAsync(Guid workflowId, int versionNumber, CancellationToken ct) =>
        await db.Set<WorkflowVersion>().FirstOrDefaultAsync(v => v.WorkflowId == workflowId && v.VersionNumber == versionNumber, ct)
            ?? throw new WorkflowRegistryException("Workflow version not found.");

    private static string Canonicalize(string definitionJson)
    {
        try
        {
            return WorkflowSerializer.Canonicalize(definitionJson);
        }
        catch (WorkflowSerializationException ex)
        {
            throw new WorkflowRegistryException(ex.Message);
        }
    }

    private static WorkflowVersion NewDraft(Guid workflowId, int number, string canonicalJson, string? notes, string userId) => new()
    {
        WorkflowId = workflowId,
        VersionNumber = number,
        Status = "draft",
        SchemaVersion = 1,
        DefinitionJson = canonicalJson,
        SnapshotHash = WorkflowSerializer.Hash(canonicalJson),
        Notes = notes,
        CreatedByUserId = userId,
        CreatedUtc = DateTime.UtcNow,
    };

    // The portable export envelope. Definition is embedded as real JSON, not a string.
    private sealed record WorkflowExport(string Kind, int SchemaVersion, string Name, WorkflowDefinition Definition, string SnapshotHash);
}
