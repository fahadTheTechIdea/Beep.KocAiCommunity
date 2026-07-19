namespace Beep.KocAiCommunity.Application.Experiments;

/// <summary>A metric observation to export (one AutoML trial's result, or a manual log).</summary>
public sealed record RunMetricEntry(string Name, double Value, string? Dataset, string? Phase, int Step);

/// <summary>
/// A pluggable target for run metrics. The default <c>EfExperimentSink</c> writes to the experiment
/// tables; an optional MLflow REST adapter can be registered alongside it. The service fans metrics
/// out to every registered sink, so the export target is swappable without touching callers.
/// </summary>
public interface IExperimentSink
{
    Task RecordMetricsAsync(Guid runId, IReadOnlyList<RunMetricEntry> metrics, CancellationToken ct = default);
}
