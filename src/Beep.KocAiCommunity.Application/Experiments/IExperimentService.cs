using Beep.KocAiCommunity.Contracts.Experiments;
using Beep.KocAiCommunity.Domain.Experiments;

namespace Beep.KocAiCommunity.Application.Experiments;

/// <summary>How a run is created and finalized (used by the training job handler).</summary>
public sealed record StartRunRequest(Guid ExperimentId, string RunByUserId, string Task, Guid? DatasetId, Guid? ParentRunId);

public sealed record FinishRunRequest(
    string Status, string? Algorithm, string? PrimaryMetric, double? PrimaryValue,
    string? SecondaryMetric, double? SecondaryValue, long RowCount, int TrialCount,
    string? HyperparametersJson, string? EnvironmentJson, string? DatasetSnapshotHash, string? FailureStage,
    Guid? ModelRunId = null);

/// <summary>
/// Experiment tracking: experiments and their runs, live metric ingestion (fanned to every
/// <see cref="IExperimentSink"/>), parameters, best-run selection, and comparison.
/// </summary>
public interface IExperimentService
{
    Task<ExperimentDto> CreateAsync(string userId, CreateExperimentRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<ExperimentDto>> ListForOwnerAsync(string userId, CancellationToken ct = default);
    Task<ExperimentDto?> GetAsync(Guid experimentId, CancellationToken ct = default);

    Task<Guid> StartRunAsync(StartRunRequest request, CancellationToken ct = default);
    Task FinishRunAsync(Guid runId, FinishRunRequest request, CancellationToken ct = default);

    Task<IReadOnlyList<RunDto>> ListRunsAsync(Guid experimentId, CancellationToken ct = default);
    Task<RunDto?> GetRunAsync(Guid runId, CancellationToken ct = default);
    Task<RunDto?> UpdateRunAsync(Guid runId, UpdateRunRequest request, CancellationToken ct = default);

    /// <summary>Record metrics for a run; fans out to all sinks. No-op if the run isn't running.</summary>
    Task LogMetricsAsync(Guid runId, IReadOnlyList<RunMetricEntry> metrics, CancellationToken ct = default);
    Task<IReadOnlyList<RunMetricDto>> GetMetricsAsync(Guid runId, CancellationToken ct = default);

    Task LogParameterAsync(Guid runId, string name, string valueJson, CancellationToken ct = default);
    Task<IReadOnlyList<RunParameterDto>> GetParametersAsync(Guid runId, CancellationToken ct = default);

    Task<RunDto?> GetBestRunAsync(Guid experimentId, CancellationToken ct = default);
    Task<IReadOnlyList<ComparisonRowDto>> CompareAsync(Guid experimentId, CancellationToken ct = default);
}
