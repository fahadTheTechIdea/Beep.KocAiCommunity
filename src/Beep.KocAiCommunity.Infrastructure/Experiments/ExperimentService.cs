using Beep.KocAiCommunity.Application.Experiments;
using Beep.KocAiCommunity.Contracts.Experiments;
using Beep.KocAiCommunity.Domain.Experiments;
using Beep.KocAiCommunity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Beep.KocAiCommunity.Infrastructure.Experiments;

/// <summary>
/// Experiment tracking over EF Core. Metric ingestion fans out to every registered
/// <see cref="IExperimentSink"/>, so the export target (our tables, and optionally MLflow) is
/// swappable without changing callers. Best-run is the highest primary metric among completed runs
/// (all primary metrics here — Accuracy, MicroAccuracy, R² — are higher-is-better).
/// </summary>
public sealed class ExperimentService(KocDbContext db, IEnumerable<IExperimentSink> sinks) : IExperimentService
{
    public async Task<ExperimentDto> CreateAsync(string userId, CreateExperimentRequest request, CancellationToken ct = default)
    {
        var experiment = new Experiment
        {
            Name = request.Name,
            Description = request.Description ?? "",
            OwnerUserId = userId,
            ProjectId = request.ProjectId,
            Tags = request.Tags,
            Status = "active",
            CreatedByUserId = userId,
            CreatedUtc = DateTime.UtcNow,
        };
        db.Set<Experiment>().Add(experiment);
        await db.SaveChangesAsync(ct);
        return ToDto(experiment, 0);
    }

    public async Task<IReadOnlyList<ExperimentDto>> ListForOwnerAsync(string userId, CancellationToken ct = default)
    {
        var experiments = await db.Set<Experiment>().AsNoTracking()
            .Where(e => e.OwnerUserId == userId)
            .OrderByDescending(e => e.CreatedUtc)
            .ToListAsync(ct);

        var counts = await db.Set<Run>().AsNoTracking()
            .Where(r => experiments.Select(e => e.Id).Contains(r.ExperimentId))
            .GroupBy(r => r.ExperimentId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        return [.. experiments.Select(e => ToDto(e, counts.GetValueOrDefault(e.Id, 0)))];
    }

    public async Task<ExperimentDto?> GetAsync(Guid experimentId, CancellationToken ct = default)
    {
        var experiment = await db.Set<Experiment>().AsNoTracking().FirstOrDefaultAsync(e => e.Id == experimentId, ct);
        if (experiment is null)
        {
            return null;
        }

        var count = await db.Set<Run>().CountAsync(r => r.ExperimentId == experimentId, ct);
        return ToDto(experiment, count);
    }

    public async Task<Guid> StartRunAsync(StartRunRequest request, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var run = new Run
        {
            ExperimentId = request.ExperimentId,
            ParentRunId = request.ParentRunId,
            DatasetId = request.DatasetId,
            RunByUserId = request.RunByUserId,
            Task = request.Task,
            Status = "running",
            StartedUtc = now,
            CreatedByUserId = request.RunByUserId,
            CreatedUtc = now,
        };
        db.Set<Run>().Add(run);
        await db.SaveChangesAsync(ct);
        return run.Id;
    }

    public async Task FinishRunAsync(Guid runId, FinishRunRequest request, CancellationToken ct = default)
    {
        var run = await db.Set<Run>().FirstOrDefaultAsync(r => r.Id == runId, ct);
        if (run is null)
        {
            return;
        }

        run.Status = request.Status;
        run.FailureStage = request.FailureStage;
        run.Algorithm = request.Algorithm;
        run.PrimaryMetric = request.PrimaryMetric;
        run.PrimaryValue = request.PrimaryValue;
        run.SecondaryMetric = request.SecondaryMetric;
        run.SecondaryValue = request.SecondaryValue;
        run.RowCount = request.RowCount;
        run.TrialCount = request.TrialCount;
        run.HyperparametersJson = request.HyperparametersJson;
        run.EnvironmentJson = request.EnvironmentJson;
        run.DatasetSnapshotHash = request.DatasetSnapshotHash;
        run.ModelRunId = request.ModelRunId ?? run.ModelRunId;
        run.CompletedUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        await RecomputeBestRunAsync(run.ExperimentId, ct);
    }

    public async Task<IReadOnlyList<RunDto>> ListRunsAsync(Guid experimentId, CancellationToken ct = default) =>
        [.. (await db.Set<Run>().AsNoTracking()
            .Where(r => r.ExperimentId == experimentId)
            .OrderByDescending(r => r.CreatedUtc)
            .ToListAsync(ct)).Select(ToRunDto)];

    public async Task<RunDto?> GetRunAsync(Guid runId, CancellationToken ct = default)
    {
        var run = await db.Set<Run>().AsNoTracking().FirstOrDefaultAsync(r => r.Id == runId, ct);
        return run is null ? null : ToRunDto(run);
    }

    public async Task<RunDto?> UpdateRunAsync(Guid runId, UpdateRunRequest request, CancellationToken ct = default)
    {
        var run = await db.Set<Run>().FirstOrDefaultAsync(r => r.Id == runId, ct);
        if (run is null)
        {
            return null;
        }

        if (request.IsFavorite is { } fav)
        {
            run.IsFavorite = fav;
        }

        if (request.Tags is not null)
        {
            run.Tags = request.Tags;
        }

        // Marking a run best is authoritative: clear the others and point the experiment at it.
        if (request.IsBest is true)
        {
            var siblings = await db.Set<Run>().Where(r => r.ExperimentId == run.ExperimentId).ToListAsync(ct);
            foreach (var s in siblings)
            {
                s.IsBest = s.Id == runId;
            }

            var experiment = await db.Set<Experiment>().FirstOrDefaultAsync(e => e.Id == run.ExperimentId, ct);
            if (experiment is not null)
            {
                experiment.BestRunId = runId;
            }
        }
        else if (request.IsBest is false)
        {
            run.IsBest = false;
        }

        await db.SaveChangesAsync(ct);
        return ToRunDto(run);
    }

    public async Task LogMetricsAsync(Guid runId, IReadOnlyList<RunMetricEntry> metrics, CancellationToken ct = default)
    {
        if (metrics.Count == 0)
        {
            return;
        }

        var status = await db.Set<Run>().AsNoTracking().Where(r => r.Id == runId).Select(r => r.Status).FirstOrDefaultAsync(ct);
        if (status != "running")
        {
            return; // metrics are only accepted while a run is active
        }

        foreach (var sink in sinks)
        {
            await sink.RecordMetricsAsync(runId, metrics, ct);
        }
    }

    public async Task<IReadOnlyList<RunMetricDto>> GetMetricsAsync(Guid runId, CancellationToken ct = default) =>
        [.. (await db.Set<RunMetric>().AsNoTracking()
            .Where(m => m.RunId == runId)
            .OrderBy(m => m.Step).ThenBy(m => m.LoggedUtc)
            .ToListAsync(ct)).Select(m => new RunMetricDto(m.Name, m.Value, m.Dataset, m.Phase, m.Step, m.LoggedUtc))];

    public async Task LogParameterAsync(Guid runId, string name, string valueJson, CancellationToken ct = default)
    {
        var existing = await db.Set<RunParameter>().FirstOrDefaultAsync(p => p.RunId == runId && p.Name == name, ct);
        if (existing is null)
        {
            db.Set<RunParameter>().Add(new RunParameter { RunId = runId, Name = name, ValueJson = valueJson, CreatedUtc = DateTime.UtcNow });
        }
        else
        {
            existing.ValueJson = valueJson;
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<RunParameterDto>> GetParametersAsync(Guid runId, CancellationToken ct = default) =>
        [.. (await db.Set<RunParameter>().AsNoTracking()
            .Where(p => p.RunId == runId)
            .OrderBy(p => p.Name)
            .ToListAsync(ct)).Select(p => new RunParameterDto(p.Name, p.ValueJson))];

    public async Task<RunDto?> GetBestRunAsync(Guid experimentId, CancellationToken ct = default)
    {
        var run = await db.Set<Run>().AsNoTracking()
            .Where(r => r.ExperimentId == experimentId && r.IsBest)
            .FirstOrDefaultAsync(ct);
        return run is null ? null : ToRunDto(run);
    }

    public async Task<IReadOnlyList<ComparisonRowDto>> CompareAsync(Guid experimentId, CancellationToken ct = default) =>
        [.. (await db.Set<Run>().AsNoTracking()
            .Where(r => r.ExperimentId == experimentId && r.Status == "completed")
            .OrderByDescending(r => r.PrimaryValue)
            .ToListAsync(ct))
            .Select(r => new ComparisonRowDto(r.Id, r.Algorithm, r.Status, r.PrimaryMetric, r.PrimaryValue, r.SecondaryValue, r.IsBest))];

    private async Task RecomputeBestRunAsync(Guid experimentId, CancellationToken ct)
    {
        var runs = await db.Set<Run>()
            .Where(r => r.ExperimentId == experimentId && r.Status == "completed" && r.PrimaryValue != null)
            .ToListAsync(ct);
        if (runs.Count == 0)
        {
            return;
        }

        var best = runs.OrderByDescending(r => r.PrimaryValue).First();
        foreach (var r in runs)
        {
            r.IsBest = r.Id == best.Id;
        }

        var experiment = await db.Set<Experiment>().FirstOrDefaultAsync(e => e.Id == experimentId, ct);
        if (experiment is not null)
        {
            experiment.BestRunId = best.Id;
        }

        await db.SaveChangesAsync(ct);
    }

    private static ExperimentDto ToDto(Experiment e, int runCount) =>
        new(e.Id, e.Name, e.Description, e.OwnerUserId, e.ProjectId, e.Status, e.BestRunId, e.Tags, runCount, e.CreatedUtc);

    private static RunDto ToRunDto(Run r) => new(
        r.Id, r.ExperimentId, r.ParentRunId, r.RunByUserId, r.Status, r.Task, r.Algorithm,
        r.PrimaryMetric, r.PrimaryValue, r.SecondaryMetric, r.SecondaryValue, r.RowCount, r.TrialCount,
        r.IsFavorite, r.IsBest, r.Tags, r.ModelRunId, r.StartedUtc, r.CompletedUtc, r.CreatedUtc);
}
