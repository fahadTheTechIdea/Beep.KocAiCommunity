using Beep.KocAiCommunity.Application.ML;
using Beep.KocAiCommunity.Application.Storage;
using Beep.KocAiCommunity.Domain.Common;
using Beep.KocAiCommunity.Domain.Studio;
using Beep.KocAiCommunity.Infrastructure.Persistence;

namespace Beep.KocAiCommunity.Infrastructure.Studio;

/// <summary>
/// Persists a captured ML.NET model as a governed artifact and records a registerable
/// <see cref="ModelRun"/>. Shared by the durable training handlers so every trained result — whether
/// from a plain train job, an experiment, or a workflow run — is registerable in the model registry.
/// </summary>
public static class ModelRunRecorder
{
    public static async Task<ModelRun> RecordAsync(
        KocDbContext db, IArtifactService artifacts, CapturedModel captured,
        string datasetName, string labelColumn, KocDataClassification classification, string ownerUserId, CancellationToken ct = default)
    {
        var result = captured.Result;
        using var modelStream = new MemoryStream(captured.ModelBytes);
        var reference = await artifacts.SaveAsync(modelStream, $"models/{Guid.NewGuid():N}.zip", "application/zip", classification, ct);

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
            RunByUserId = ownerUserId,
            CompletedUtc = DateTime.UtcNow,
            FeatureStatsJson = captured.FeatureStatsJson,
            ModelArtifactId = reference.Id,
            CreatedByUserId = ownerUserId,
            CreatedUtc = DateTime.UtcNow,
        };
        db.Set<ModelRun>().Add(run);
        await db.SaveChangesAsync(ct);
        return run;
    }
}
