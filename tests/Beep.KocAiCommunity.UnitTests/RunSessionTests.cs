using Beep.KocAiCommunity.Contracts.Workflow;
using Beep.KocAiCommunity.Ui.Studio.Services;
using FluentAssertions;
using Xunit;

namespace Beep.KocAiCommunity.UnitTests;

/// <summary>
/// The designer's run results, held outside the page that renders them.
/// <para>
/// They used to be component state, so navigating to Datasets and back lost the run you were reading.
/// The subtle requirement is the replacement rule: a node that did not execute this time must not still
/// be showing the last run's data beside this run's metric.
/// </para>
/// </summary>
public class RunSessionTests
{
    private static PipelineExecutionResult Run(params NodeExecutionResult[] nodes) =>
        new(true, "LightGbm", "Accuracy", 0.93, nodes);

    private static NodeExecutionResult Node(string id, long rowsOut = 100) =>
        new(id, "filter", "done", "", RowsIn: 120, RowsOut: rowsOut, ElapsedMs: 12,
            Sample: new NodeSample(["a", "b"], [["1", "2"]], 2, rowsOut));

    [Fact]
    public void A_recorded_run_is_readable_per_node()
    {
        var session = new RunSession();

        session.Record(Run(Node("n1"), Node("n2", 80)));

        session.ResultFor("n1")!.RowsOut.Should().Be(100);
        session.ResultFor("n2")!.RowsOut.Should().Be(80);
        session.Last!.PrimaryValue.Should().Be(0.93);
    }

    [Fact]
    public void A_node_that_did_not_run_this_time_has_no_stale_result()
    {
        // Showing the previous run's rows under this run's metric is how somebody reaches a confident
        // wrong conclusion about what their pipeline did.
        var session = new RunSession();
        session.Record(Run(Node("n1"), Node("removed")));

        session.Record(Run(Node("n1", 50)));

        session.ResultFor("n1")!.RowsOut.Should().Be(50);
        session.ResultFor("removed").Should().BeNull();
    }

    [Fact]
    public void An_unknown_node_simply_has_nothing()
    {
        new RunSession().ResultFor("never-ran").Should().BeNull();
    }

    [Fact]
    public void A_compile_failure_with_no_node_id_is_not_indexed()
    {
        // The compiler reports its failure as a node result with an empty id; keying on that would
        // make an empty selection appear to have data.
        var session = new RunSession();

        session.Record(new PipelineExecutionResult(false, null, null, 0,
            [new NodeExecutionResult("", "compile", "failed", "no dataset")]));

        session.ResultFor("").Should().BeNull();
        session.Last!.Success.Should().BeFalse();
    }

    [Fact]
    public void The_log_is_bounded()
    {
        // It outlives the page, so an unbounded list that never resets is a slow leak.
        var session = new RunSession();
        for (var i = 0; i < RunSession.MaxLogEntries + 50; i++)
        {
            session.Add("info", $"line {i}");
        }

        session.Log.Should().HaveCount(RunSession.MaxLogEntries);
        session.Log[^1].Message.Should().Be($"line {RunSession.MaxLogEntries + 49}");
        session.Log[0].Message.Should().Be("line 50", "the oldest are dropped, not the newest");
    }

    [Fact]
    public void Clearing_the_log_keeps_the_last_run()
    {
        // Two different intentions: "this log is noisy" is not "forget what my pipeline did".
        var session = new RunSession();
        session.Record(Run(Node("n1")));
        session.Add("info", "something");

        session.ClearLog();

        session.Log.Should().BeEmpty();
        session.ResultFor("n1").Should().NotBeNull();
        session.Last.Should().NotBeNull();
    }

    [Fact]
    public void Reset_forgets_everything()
    {
        var session = new RunSession();
        session.Record(Run(Node("n1")));
        session.Add("info", "something");

        session.Reset();

        session.Log.Should().BeEmpty();
        session.Last.Should().BeNull();
        session.ResultFor("n1").Should().BeNull();
    }
}
