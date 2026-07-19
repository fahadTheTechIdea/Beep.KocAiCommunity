using System.Diagnostics;
using System.Text.Json;
using Beep.KocAiCommunity.Application.ML;
using Beep.KocAiCommunity.Application.Storage;
using Beep.KocAiCommunity.Application.Studio;
using Beep.KocAiCommunity.Domain.Studio;
using Beep.KocAiCommunity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Beep.KocAiCommunity.Infrastructure.Studio;

/// <summary>
/// Serves predictions from registered model versions. Resolves a version to its training run and
/// saved model artifact, scores through the <see cref="IPredictionPool"/>, and records a
/// <see cref="ModelInferenceLog"/> for every call (including failures).
/// </summary>
public sealed class InferenceService(KocDbContext db, IArtifactService artifacts, IPredictionPool pool) : IInferenceService
{
    // Drift is flagged when a feature's batch mean shifts by more than this fraction of the
    // baseline range (max - min). A pragmatic, explainable threshold — not a statistical test.
    private const double DriftFraction = 0.25;

    public async Task<InferenceResult> InferAsync(
        string callerUserId,
        bool isPlatformAdmin,
        Guid modelVersionId,
        string endpoint,
        IReadOnlyList<IReadOnlyDictionary<string, string>> rows,
        CancellationToken ct = default)
    {
        if (rows.Count == 0)
        {
            throw new InferenceException("Provide at least one input row.");
        }

        var (version, run) = await ResolveServableAsync(callerUserId, isPlatformAdmin, modelVersionId, ct);
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var result = await pool.PredictAsync(
                version.Id,
                token => LoadModelBytesAsync(run.ModelArtifactId!.Value, token),
                run.LabelColumn,
                rows,
                ct);
            stopwatch.Stop();
            await LogAsync(version.Id, callerUserId, endpoint, rows.Count, (int)stopwatch.ElapsedMilliseconds, success: true, error: null, ct);
            return result;
        }
        catch (Exception ex) when (ex is not InferenceException)
        {
            stopwatch.Stop();
            await LogAsync(version.Id, callerUserId, endpoint, rows.Count, (int)stopwatch.ElapsedMilliseconds, success: false, error: ex.Message, ct);
            throw new InferenceException($"Inference failed: {ex.Message}");
        }
    }

    public async Task<IReadOnlyList<ModelInferenceLog>> GetLogsAsync(string callerUserId, bool isPlatformAdmin, Guid modelVersionId, int take = 50, CancellationToken ct = default)
    {
        var (version, _) = await ResolveAsync(modelVersionId, ct);
        await EnsureOwnerOrAdminAsync(callerUserId, isPlatformAdmin, version, ct);

        return await db.Set<ModelInferenceLog>().AsNoTracking()
            .Where(l => l.ModelVersionId == modelVersionId)
            .OrderByDescending(l => l.CalledUtc)
            .Take(Math.Clamp(take, 1, 500))
            .ToListAsync(ct);
    }

    public async Task<DriftReport> ComputeDriftAsync(
        string callerUserId,
        bool isPlatformAdmin,
        Guid modelVersionId,
        IReadOnlyList<IReadOnlyDictionary<string, string>> rows,
        CancellationToken ct = default)
    {
        var (_, run) = await ResolveServableAsync(callerUserId, isPlatformAdmin, modelVersionId, ct);

        var baseline = run.FeatureStatsJson is null
            ? null
            : JsonSerializer.Deserialize<FeatureStatsDoc>(run.FeatureStatsJson);
        if (baseline is null || baseline.Features.Count == 0)
        {
            throw new InferenceException("This model has no drift baseline (retrain to capture one).");
        }

        var drifts = new List<FeatureDrift>();
        foreach (var (feature, stat) in baseline.Features)
        {
            double sum = 0;
            var n = 0;
            foreach (var row in rows)
            {
                if (row.TryGetValue(feature, out var raw) && double.TryParse(raw, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var v))
                {
                    sum += v;
                    n++;
                }
            }

            if (n == 0)
            {
                continue; // feature absent from the batch
            }

            var batchMean = sum / n;
            var shift = Math.Abs(batchMean - stat.Mean);
            var range = Math.Max(stat.Max - stat.Min, 1e-9);
            var drifted = shift > DriftFraction * range;
            drifts.Add(new FeatureDrift(feature, stat.Mean, batchMean, shift, drifted));
        }

        return new DriftReport(baseline.RowCount, rows.Count, drifts, drifts.Any(d => d.Drifted));
    }

    private async Task<(ModelVersion Version, ModelRun Run)> ResolveAsync(Guid modelVersionId, CancellationToken ct)
    {
        var version = await db.Set<ModelVersion>().AsNoTracking().FirstOrDefaultAsync(v => v.Id == modelVersionId, ct)
            ?? throw new InferenceException("Model version not found.");
        var run = await db.Set<ModelRun>().AsNoTracking().FirstOrDefaultAsync(r => r.Id == version.SourceRunId, ct)
            ?? throw new InferenceException("The source training run for this version is missing.");
        return (version, run);
    }

    private async Task<(ModelVersion Version, ModelRun Run)> ResolveServableAsync(string callerUserId, bool isPlatformAdmin, Guid modelVersionId, CancellationToken ct)
    {
        var (version, run) = await ResolveAsync(modelVersionId, ct);

        if (run.ModelArtifactId is null)
        {
            throw new InferenceException("This version has no saved model to serve (it was trained before model persistence — retrain to enable inference).");
        }

        // Production versions are servable to any employee; others only to the owner or an admin.
        if (version.Status != "production")
        {
            await EnsureOwnerOrAdminAsync(callerUserId, isPlatformAdmin, version, ct);
        }

        return (version, run);
    }

    private async Task EnsureOwnerOrAdminAsync(string callerUserId, bool isPlatformAdmin, ModelVersion version, CancellationToken ct)
    {
        if (isPlatformAdmin || version.RegisteredByUserId == callerUserId)
        {
            return;
        }

        var ownerId = await db.Set<RegisteredModel>().AsNoTracking()
            .Where(m => m.Id == version.ModelId).Select(m => m.OwnerUserId).FirstOrDefaultAsync(ct);
        if (ownerId != callerUserId)
        {
            throw new InferenceException("Only the model owner or a platform admin can use a non-production version.");
        }
    }

    private async Task<byte[]> LoadModelBytesAsync(Guid artifactId, CancellationToken ct)
    {
        await using var stream = await artifacts.OpenReadAsync(artifactId, ct);
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct);
        return ms.ToArray();
    }

    private async Task LogAsync(Guid versionId, string caller, string endpoint, int rows, int latencyMs, bool success, string? error, CancellationToken ct)
    {
        db.Set<ModelInferenceLog>().Add(new ModelInferenceLog
        {
            ModelVersionId = versionId,
            CallerUserId = caller,
            Endpoint = endpoint,
            RowCount = rows,
            LatencyMs = latencyMs,
            CalledUtc = DateTime.UtcNow,
            Success = success,
            Error = error,
            CreatedByUserId = caller,
            CreatedUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(ct);
    }

    // Mirrors the shape written by AutoMlTrainer.ComputeFeatureStats.
    private sealed record FeatureStat(long Count, double Mean, double Min, double Max);
    private sealed record FeatureStatsDoc(long RowCount, Dictionary<string, FeatureStat> Features);
}
