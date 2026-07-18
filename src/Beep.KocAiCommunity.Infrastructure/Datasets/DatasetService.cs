using Beep.KocAiCommunity.Application.Authorization;
using Beep.KocAiCommunity.Application.Datasets;
using Beep.KocAiCommunity.Application.Organization;
using Beep.KocAiCommunity.Domain.Common;
using Beep.KocAiCommunity.Domain.Datasets;
using Beep.KocAiCommunity.Domain.Organization;
using Beep.KocAiCommunity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Beep.KocAiCommunity.Infrastructure.Datasets;

public sealed class DatasetService(
    KocDbContext db,
    IOrgDirectory directory,
    IVisibilityEvaluator visibility) : IDatasetService
{
    public async Task<Dataset> CreateAsync(
        string userId, string name, string description, VisibilityScope scope,
        KocDataClassification classification, string domain, string? tags, CancellationToken ct = default)
    {
        var unitId = await directory.ResolveScopeUnitAsync(userId, scope, ct);
        if (scope != VisibilityScope.Company && unitId is null)
        {
            throw new DatasetException($"You are not part of an org unit at the '{scope}' level, so you can't scope a dataset to it.");
        }

        var dataset = new Dataset
        {
            Name = name,
            Description = description,
            OwnerUserId = userId,
            VisibilityScope = scope,
            VisibilityOrgUnitId = unitId ?? Guid.Empty,
            Classification = classification,
            Domain = domain,
            Tags = tags,
            CreatedByUserId = userId,
            CreatedUtc = DateTime.UtcNow,
        };

        db.Set<Dataset>().Add(dataset);
        await db.SaveChangesAsync(ct);
        return dataset;
    }

    public async Task<IReadOnlyList<Dataset>> BrowseVisibleAsync(string userId, CancellationToken ct = default)
    {
        var all = await db.Set<Dataset>().AsNoTracking()
            .OrderByDescending(d => d.CreatedUtc)
            .ToListAsync(ct);

        var visible = new List<Dataset>(all.Count);
        foreach (var dataset in all)
        {
            if (dataset.OwnerUserId == userId
                || await visibility.CanSeeAsync(userId, dataset.VisibilityScope, dataset.VisibilityOrgUnitId, ct))
            {
                visible.Add(dataset);
            }
        }

        return visible;
    }

    public async Task<Dataset?> GetVisibleAsync(string userId, Guid datasetId, CancellationToken ct = default)
    {
        var dataset = await db.Set<Dataset>().AsNoTracking().FirstOrDefaultAsync(d => d.Id == datasetId, ct);
        if (dataset is null)
        {
            return null;
        }

        var canSee = dataset.OwnerUserId == userId
            || await visibility.CanSeeAsync(userId, dataset.VisibilityScope, dataset.VisibilityOrgUnitId, ct);
        return canSee ? dataset : null;
    }
}
