using Beep.KocAiCommunity.Ui.Studio.Services;
using FluentAssertions;
using Xunit;

namespace Beep.KocAiCommunity.UnitTests;

/// <summary>
/// Undo and redo over whole-graph snapshots.
/// <para>
/// Snapshots rather than a command log on purpose: a per-edit log has to model every operation the
/// canvas can do — move, connect, disconnect, retype a property — and will get one of them subtly
/// wrong. These pin the two things that make a snapshot stack behave: it is bounded, and recording
/// after an undo abandons the future rather than offering a state the graph can no longer reach.
/// </para>
/// </summary>
public class GraphHistoryTests
{
    [Fact]
    public void Nothing_to_undo_before_a_second_state_is_recorded()
    {
        var history = new GraphHistory();

        history.CanUndo.Should().BeFalse();
        history.CanRedo.Should().BeFalse();
        history.Undo().Should().BeNull();

        history.Record("a");

        history.CanUndo.Should().BeFalse("there is nowhere to go back to from the first state");
    }

    [Fact]
    public void Undo_returns_the_previous_state_exactly()
    {
        var history = new GraphHistory();
        history.Record("one");
        history.Record("two");
        history.Record("three");

        history.Undo().Should().Be("two");
        history.Undo().Should().Be("one");
        history.CanUndo.Should().BeFalse();
        history.Current.Should().Be("one");
    }

    [Fact]
    public void Redo_returns_what_was_undone()
    {
        var history = new GraphHistory();
        history.Record("one");
        history.Record("two");

        history.Undo().Should().Be("one");
        history.CanRedo.Should().BeTrue();
        history.Redo().Should().Be("two");
        history.CanRedo.Should().BeFalse();
    }

    [Fact]
    public void Recording_after_an_undo_abandons_the_redo_future()
    {
        // The graph is no longer where that future branched from, so redoing into it would restore an
        // edit made to a graph that no longer exists.
        var history = new GraphHistory();
        history.Record("one");
        history.Record("two");
        history.Record("three");
        history.Undo();

        history.Record("different");

        history.CanRedo.Should().BeFalse();
        history.Current.Should().Be("different");
        history.Undo().Should().Be("two");
    }

    [Fact]
    public void Recording_the_same_state_twice_does_not_add_an_entry()
    {
        // The designer records on a debounce, which fires whether or not anything actually changed.
        // Without this, undo would appear to do nothing for several presses.
        var history = new GraphHistory();
        history.Record("same");
        history.Record("same");
        history.Record("same");

        history.Count.Should().Be(1);
        history.CanUndo.Should().BeFalse();
    }

    [Fact]
    public void The_stack_is_bounded_and_keeps_the_most_recent()
    {
        var history = new GraphHistory();
        for (var i = 0; i < GraphHistory.Capacity + 20; i++)
        {
            history.Record($"state-{i}");
        }

        history.Count.Should().Be(GraphHistory.Capacity);
        history.Current.Should().Be($"state-{GraphHistory.Capacity + 19}");
        history.Undo().Should().Be($"state-{GraphHistory.Capacity + 18}");
    }

    [Fact]
    public void Undoing_to_the_bottom_of_a_trimmed_stack_stops_rather_than_running_off()
    {
        var history = new GraphHistory();
        for (var i = 0; i < GraphHistory.Capacity + 5; i++)
        {
            history.Record($"state-{i}");
        }

        for (var i = 0; i < GraphHistory.Capacity + 20; i++)
        {
            history.Undo();
        }

        history.CanUndo.Should().BeFalse();
        history.Current.Should().Be("state-5", "the oldest five were trimmed away");
    }

    [Fact]
    public void Reset_starts_again_from_one_state()
    {
        // Loading a different workflow. Carrying the old stack would let Ctrl+Z restore a graph
        // belonging to something else entirely.
        var history = new GraphHistory();
        history.Record("old-one");
        history.Record("old-two");

        history.Reset("new-workflow");

        history.Count.Should().Be(1);
        history.Current.Should().Be("new-workflow");
        history.CanUndo.Should().BeFalse();
        history.CanRedo.Should().BeFalse();
    }
}
