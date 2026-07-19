using Beep.KocAiCommunity.Application.Experiments;
using Beep.KocAiCommunity.Domain.Experiments;
using Beep.KocAiCommunity.Infrastructure.Persistence;

namespace Beep.KocAiCommunity.Infrastructure.Experiments;

/// <summary>The default metric sink: writes each observation as a <see cref="RunMetric"/> row.</summary>
public sealed class EfExperimentSink(KocDbContext db) : IExperimentSink
{
    public async Task RecordMetricsAsync(Guid runId, IReadOnlyList<RunMetricEntry> metrics, CancellationToken ct = default)
    {
        if (metrics.Count == 0)
        {
            return;
        }

        var now = DateTime.UtcNow;
        db.Set<RunMetric>().AddRange(metrics.Select(m => new RunMetric
        {
            RunId = runId,
            Name = m.Name,
            Value = m.Value,
            Dataset = m.Dataset,
            Phase = m.Phase,
            Step = m.Step,
            LoggedUtc = now,
            CreatedUtc = now,
        }));
        await db.SaveChangesAsync(ct);
    }
}
