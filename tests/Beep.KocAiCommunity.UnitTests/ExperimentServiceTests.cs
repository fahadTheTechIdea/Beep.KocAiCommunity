using Beep.KocAiCommunity.Application.Experiments;
using Beep.KocAiCommunity.Contracts.Experiments;
using Beep.KocAiCommunity.Infrastructure.Experiments;
using FluentAssertions;
using Xunit;

namespace Beep.KocAiCommunity.UnitTests;

public class ExperimentServiceTests
{
    // A second sink proves metrics fan out to every registered target (the MLflow-swap contract).
    private sealed class SpySink : IExperimentSink
    {
        public List<RunMetricEntry> Received { get; } = [];
        public Task RecordMetricsAsync(Guid runId, IReadOnlyList<RunMetricEntry> metrics, CancellationToken ct = default)
        {
            Received.AddRange(metrics);
            return Task.CompletedTask;
        }
    }

    private static (ExperimentService svc, SpySink spy) Build(OrgTestContext ctx)
    {
        var spy = new SpySink();
        var svc = new ExperimentService(ctx.Db, [new EfExperimentSink(ctx.Db), spy]);
        return (svc, spy);
    }

    private static FinishRunRequest Finished(double primary) =>
        new("completed", "FastTree", "Accuracy", primary, "AUC", 0.9, 100, 3, "{}", "{}", "abc", null);

    [Fact]
    public async Task A_run_records_metrics_to_every_sink()
    {
        using var ctx = new OrgTestContext();
        var (svc, spy) = Build(ctx);
        var exp = await svc.CreateAsync("emp1", new CreateExperimentRequest("Pumps", "", null, null));
        var runId = await svc.StartRunAsync(new StartRunRequest(exp.Id, "emp1", "BinaryClassification", null, null));

        await svc.LogMetricsAsync(runId, [new RunMetricEntry("Accuracy", 0.7, "validation", "trial", 1)]);
        await svc.LogMetricsAsync(runId, [new RunMetricEntry("Accuracy", 0.82, "validation", "trial", 2)]);

        var metrics = await svc.GetMetricsAsync(runId);
        metrics.Should().HaveCount(2);                  // persisted by the EF sink
        spy.Received.Should().HaveCount(2);             // and fanned out to the spy sink
    }

    [Fact]
    public async Task Metrics_are_rejected_once_a_run_is_finished()
    {
        using var ctx = new OrgTestContext();
        var (svc, _) = Build(ctx);
        var exp = await svc.CreateAsync("emp1", new CreateExperimentRequest("Pumps", "", null, null));
        var runId = await svc.StartRunAsync(new StartRunRequest(exp.Id, "emp1", "BinaryClassification", null, null));
        await svc.FinishRunAsync(runId, Finished(0.8));

        await svc.LogMetricsAsync(runId, [new RunMetricEntry("Accuracy", 0.99, null, null, 99)]);

        (await svc.GetMetricsAsync(runId)).Should().BeEmpty();
    }

    [Fact]
    public async Task Best_run_is_the_highest_primary_metric_and_updates_the_experiment()
    {
        using var ctx = new OrgTestContext();
        var (svc, _) = Build(ctx);
        var exp = await svc.CreateAsync("emp1", new CreateExperimentRequest("Pumps", "", null, null));

        var run1 = await svc.StartRunAsync(new StartRunRequest(exp.Id, "emp1", "BinaryClassification", null, null));
        await svc.FinishRunAsync(run1, Finished(0.80));
        var run2 = await svc.StartRunAsync(new StartRunRequest(exp.Id, "emp1", "BinaryClassification", null, null));
        await svc.FinishRunAsync(run2, Finished(0.91));

        var best = await svc.GetBestRunAsync(exp.Id);
        best!.Id.Should().Be(run2);
        best.IsBest.Should().BeTrue();

        var refreshed = await svc.GetAsync(exp.Id);
        refreshed!.BestRunId.Should().Be(run2);
    }

    [Fact]
    public async Task Comparison_lists_completed_runs_ranked_by_score()
    {
        using var ctx = new OrgTestContext();
        var (svc, _) = Build(ctx);
        var exp = await svc.CreateAsync("emp1", new CreateExperimentRequest("Pumps", "", null, null));
        var lo = await svc.StartRunAsync(new StartRunRequest(exp.Id, "emp1", "BinaryClassification", null, null));
        await svc.FinishRunAsync(lo, Finished(0.70));
        var hi = await svc.StartRunAsync(new StartRunRequest(exp.Id, "emp1", "BinaryClassification", null, null));
        await svc.FinishRunAsync(hi, Finished(0.95));

        var comparison = await svc.CompareAsync(exp.Id);

        comparison.Should().HaveCount(2);
        comparison[0].RunId.Should().Be(hi);           // best first
        comparison[0].IsBest.Should().BeTrue();
    }

    [Fact]
    public async Task Finished_run_captures_lineage_snapshots()
    {
        using var ctx = new OrgTestContext();
        var (svc, _) = Build(ctx);
        var exp = await svc.CreateAsync("emp1", new CreateExperimentRequest("Pumps", "", null, null));
        var runId = await svc.StartRunAsync(new StartRunRequest(exp.Id, "emp1", "Regression", null, null));

        await svc.FinishRunAsync(runId, Finished(0.88));

        var run = await svc.GetRunAsync(runId);
        run!.Algorithm.Should().Be("FastTree");
        run.Status.Should().Be("completed");
        run.TrialCount.Should().Be(3);
    }
}
