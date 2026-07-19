using Beep.KocAiCommunity.Application.ML;
using Beep.KocAiCommunity.Application.Notifications;
using Beep.KocAiCommunity.Application.Studio;
using Beep.KocAiCommunity.Domain.Studio;
using Beep.KocAiCommunity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Beep.KocAiCommunity.Infrastructure.Studio;

public sealed class ModelRegistryService(KocDbContext db, INotificationService notifications, IPredictionPool? pool = null) : IModelRegistry
{
    private const int Required = 2;

    public async Task<ModelVersion> RegisterAsync(string userId, string modelName, Guid sourceRunId, CancellationToken ct = default)
    {
        var run = await db.Set<ModelRun>().FirstOrDefaultAsync(r => r.Id == sourceRunId, ct)
            ?? throw new ModelRegistryException("Training run not found.");

        var model = await db.Set<RegisteredModel>().FirstOrDefaultAsync(m => m.Name == modelName, ct);
        if (model is null)
        {
            model = new RegisteredModel { Name = modelName, OwnerUserId = userId, CreatedByUserId = userId, CreatedUtc = DateTime.UtcNow };
            db.Add(model);
            await db.SaveChangesAsync(ct);
        }

        var count = await db.Set<ModelVersion>().CountAsync(v => v.ModelId == model.Id, ct);
        var version = new ModelVersion
        {
            ModelId = model.Id,
            SemVer = $"1.{count}.0",
            Status = "staging",
            SourceRunId = sourceRunId,
            MetricName = run.PrimaryMetric,
            MetricValue = run.PrimaryValue,
            RegisteredByUserId = userId,
            CreatedByUserId = userId,
            CreatedUtc = DateTime.UtcNow,
        };
        db.Add(version);
        await db.SaveChangesAsync(ct);
        return version;
    }

    public async Task<IReadOnlyList<ModelSummary>> ListAsync(CancellationToken ct = default)
    {
        var models = await db.Set<RegisteredModel>().AsNoTracking().OrderBy(m => m.Name).ToListAsync(ct);
        var versions = await db.Set<ModelVersion>().AsNoTracking().ToListAsync(ct);
        var approvals = await db.Set<ModelApproval>().AsNoTracking()
            .Where(a => a.Decision == "approve")
            .GroupBy(a => a.ModelVersionId)
            .Select(g => new { VersionId = g.Key, Count = g.Select(x => x.ApproverUserId).Distinct().Count() })
            .ToDictionaryAsync(x => x.VersionId, x => x.Count, ct);

        return models.Select(m => new ModelSummary(
            m,
            versions.Where(v => v.ModelId == m.Id).OrderBy(v => v.SemVer).ToList(),
            approvals)).ToList();
    }

    public async Task<ModelApproval> ApproveAsync(string userId, Guid versionId, CancellationToken ct = default)
    {
        var version = await db.Set<ModelVersion>().FirstOrDefaultAsync(v => v.Id == versionId, ct)
            ?? throw new ModelRegistryException("Model version not found.");

        if (version.RegisteredByUserId == userId)
        {
            throw new ModelRegistryException("You can't approve a version you registered.");
        }

        if (await db.Set<ModelApproval>().AnyAsync(a => a.ModelVersionId == versionId && a.ApproverUserId == userId, ct))
        {
            throw new ModelRegistryException("You have already approved this version.");
        }

        var approval = new ModelApproval
        {
            ModelVersionId = versionId,
            ApproverUserId = userId,
            Decision = "approve",
            CreatedByUserId = userId,
            CreatedUtc = DateTime.UtcNow,
        };
        db.Add(approval);
        await db.SaveChangesAsync(ct);
        return approval;
    }

    public async Task<ModelVersion> PromoteAsync(string userId, Guid versionId, CancellationToken ct = default)
    {
        var version = await db.Set<ModelVersion>().FirstOrDefaultAsync(v => v.Id == versionId, ct)
            ?? throw new ModelRegistryException("Model version not found.");

        var approvals = await db.Set<ModelApproval>()
            .Where(a => a.ModelVersionId == versionId && a.Decision == "approve")
            .Select(a => a.ApproverUserId).Distinct().CountAsync(ct);

        if (approvals < Required)
        {
            throw new ModelRegistryException($"Promotion needs {Required} approvals; this version has {approvals}.");
        }

        await SetProductionAsync(version, ct);
        return version;
    }

    public async Task<ModelVersion> RollbackAsync(string userId, Guid versionId, CancellationToken ct = default)
    {
        var version = await db.Set<ModelVersion>().FirstOrDefaultAsync(v => v.Id == versionId, ct)
            ?? throw new ModelRegistryException("Model version not found.");

        await SetProductionAsync(version, ct);
        return version;
    }

    public async Task<ModelDeployment> DeployAsync(string userId, Guid versionId, string environment, CancellationToken ct = default)
    {
        var env = string.IsNullOrWhiteSpace(environment) ? "production" : environment.Trim().ToLowerInvariant();
        var version = await db.Set<ModelVersion>().FirstOrDefaultAsync(v => v.Id == versionId, ct)
            ?? throw new ModelRegistryException("Model version not found.");

        if (version.Status != "production")
        {
            throw new ModelRegistryException("Only a promoted (production) version can be deployed.");
        }

        // Retire any active deployment of this model in the same environment.
        var siblingVersionIds = await db.Set<ModelVersion>()
            .Where(v => v.ModelId == version.ModelId).Select(v => v.Id).ToListAsync(ct);
        var actives = await db.Set<ModelDeployment>()
            .Where(d => d.Status == "active" && d.Environment == env && siblingVersionIds.Contains(d.ModelVersionId))
            .ToListAsync(ct);
        foreach (var d in actives)
        {
            d.Status = "retired";
            d.RetiredUtc = DateTime.UtcNow;
        }

        var deployment = new ModelDeployment
        {
            ModelVersionId = versionId,
            Environment = env,
            Status = "active",
            DeployedByUserId = userId,
            DeployedUtc = DateTime.UtcNow,
            CreatedByUserId = userId,
            CreatedUtc = DateTime.UtcNow,
        };
        db.Add(deployment);
        await db.SaveChangesAsync(ct);

        var model = await db.Set<RegisteredModel>().FirstOrDefaultAsync(m => m.Id == version.ModelId, ct);
        if (model is not null)
        {
            await notifications.NotifyAsync(model.OwnerUserId, "model-deployed",
                $"Model deployed: {model.Name} {version.SemVer}",
                $"{model.Name} {version.SemVer} is now serving in {env}.", "/models", ct);
        }

        return deployment;
    }

    public async Task RetireAsync(string userId, Guid deploymentId, CancellationToken ct = default)
    {
        var deployment = await db.Set<ModelDeployment>().FirstOrDefaultAsync(d => d.Id == deploymentId, ct)
            ?? throw new ModelRegistryException("Deployment not found.");

        if (deployment.Status == "active")
        {
            deployment.Status = "retired";
            deployment.RetiredUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            pool?.Evict(deployment.ModelVersionId); // drop the served model from the cache
        }
    }

    public async Task<IReadOnlyList<DeploymentSummary>> ListDeploymentsAsync(CancellationToken ct = default)
    {
        var deployments = await db.Set<ModelDeployment>().AsNoTracking()
            .OrderByDescending(d => d.DeployedUtc).ToListAsync(ct);
        var versions = await db.Set<ModelVersion>().AsNoTracking().ToListAsync(ct);
        var models = await db.Set<RegisteredModel>().AsNoTracking().ToDictionaryAsync(m => m.Id, m => m.Name, ct);

        return deployments.Select(d =>
        {
            var v = versions.FirstOrDefault(x => x.Id == d.ModelVersionId);
            var name = v is not null && models.TryGetValue(v.ModelId, out var n) ? n : "(unknown)";
            return new DeploymentSummary(d, name, v?.SemVer ?? "-", v?.MetricName ?? "-", v?.MetricValue ?? 0);
        }).ToList();
    }

    private async Task SetProductionAsync(ModelVersion version, CancellationToken ct)
    {
        var current = await db.Set<ModelVersion>()
            .Where(v => v.ModelId == version.ModelId && v.Status == "production" && v.Id != version.Id)
            .ToListAsync(ct);
        foreach (var v in current)
        {
            v.Status = "archived";
        }

        version.Status = "production";
        await db.SaveChangesAsync(ct);

        var model = await db.Set<RegisteredModel>().FirstOrDefaultAsync(m => m.Id == version.ModelId, ct);
        if (model is not null)
        {
            await notifications.NotifyAsync(model.OwnerUserId, "model-promoted",
                $"Model promoted: {model.Name} {version.SemVer}",
                $"{model.Name} {version.SemVer} is now the production version.", "/models", ct);
        }
    }
}
