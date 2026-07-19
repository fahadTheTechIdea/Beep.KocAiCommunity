using Beep.KocAiCommunity.Application.ML;
using Beep.KocAiCommunity.Application.Storage;
using Beep.KocAiCommunity.Application.Studio;
using Beep.KocAiCommunity.Domain.Common;
using Beep.KocAiCommunity.Domain.Studio;
using Beep.KocAiCommunity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Beep.KocAiCommunity.Infrastructure.Studio;

public sealed class StudioService(KocDbContext db, IMlTrainer trainer, IArtifactService artifacts) : IStudioService
{
    public async Task<ModelRun> TrainAsync(string userId, string datasetName, string labelColumn, MlTaskType task, Stream csv, int maxSeconds, CancellationToken ct = default)
    {
        // Capture the winning model and a drift baseline so the run can later be registered and served.
        var captured = await trainer.TrainAndCaptureAsync(task, csv, labelColumn, maxSeconds, ct);
        var result = captured.Result;

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
            FeatureStatsJson = captured.FeatureStatsJson,
            CreatedByUserId = userId,
            CreatedUtc = DateTime.UtcNow,
        };

        // Persist the serialized ML.NET model as a governed artifact, keyed by the run id.
        using var modelStream = new MemoryStream(captured.ModelBytes);
        var reference = await artifacts.SaveAsync(
            modelStream, $"models/{run.Id:N}.zip", "application/zip", KocDataClassification.Internal, ct);
        run.ModelArtifactId = reference.Id;

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
