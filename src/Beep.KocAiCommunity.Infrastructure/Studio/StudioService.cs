using Beep.KocAiCommunity.Application.ML;
using Beep.KocAiCommunity.Application.Studio;
using Beep.KocAiCommunity.Domain.Studio;
using Beep.KocAiCommunity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Beep.KocAiCommunity.Infrastructure.Studio;

public sealed class StudioService(KocDbContext db, IMlTrainer trainer) : IStudioService
{
    public async Task<ModelRun> TrainAsync(string userId, string datasetName, string labelColumn, MlTaskType task, Stream csv, int maxSeconds, CancellationToken ct = default)
    {
        var result = await trainer.TrainAsync(task, csv, labelColumn, maxSeconds, ct);

        var run = new ModelRun
        {
            DatasetName = datasetName,
            LabelColumn = labelColumn,
            Task = result.Task,
            Algorithm = result.Algorithm,
            PrimaryMetric = result.PrimaryMetric,
            PrimaryValue = result.PrimaryValue,
            SecondaryMetric = result.SecondaryMetric,
            SecondaryValue = result.SecondaryValue,
            RowCount = result.RowCount,
            RunByUserId = userId,
            CompletedUtc = DateTime.UtcNow,
            CreatedByUserId = userId,
            CreatedUtc = DateTime.UtcNow,
        };

        db.Set<ModelRun>().Add(run);
        await db.SaveChangesAsync(ct);
        return run;
    }

    public async Task<IReadOnlyList<ModelRun>> GetMyRunsAsync(string userId, CancellationToken ct = default) =>
        await db.Set<ModelRun>().AsNoTracking()
            .Where(r => r.RunByUserId == userId)
            .OrderByDescending(r => r.CompletedUtc)
            .ToListAsync(ct);
}
