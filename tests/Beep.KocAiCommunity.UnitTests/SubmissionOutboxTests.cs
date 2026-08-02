using System.Text;
using Beep.KocAiCommunity.Desktop.Local;
using FluentAssertions;
using Xunit;

namespace Beep.KocAiCommunity.UnitTests;

/// <summary>
/// The queue that lets an engineer submit without a network, and the service that drains it.
/// <para>
/// One rule runs through all of it: <b>nothing is ever silently discarded.</b> The work is already done
/// by the time anything reaches the outbox — the pipeline ran and the score is known — so losing it
/// because the network happened to be absent is the failure this exists to prevent.
/// </para>
/// </summary>
public sealed class SubmissionOutboxTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "koc-outbox-" + Guid.NewGuid().ToString("N"));
    private readonly LocalWorkspace _workspace;
    private readonly SubmissionOutbox _outbox;

    public SubmissionOutboxTests()
    {
        _workspace = new LocalWorkspace { RootPath = _root };
        _workspace.EnsureCreated();
        _outbox = new SubmissionOutbox(_workspace);
    }

    private static Stream Predictions(string content = "id,label\n1,A\n") =>
        new MemoryStream(Encoding.UTF8.GetBytes(content));

    private QueuedSubmission Entry(string id, DateTime? queuedUtc = null) => new()
    {
        Id = id,
        CompetitionId = Guid.NewGuid(),
        CompetitionTitle = "ESP failure",
        QueuedUtc = queuedUtc ?? DateTime.UtcNow,
        LocalScore = 0.87,
        CompetitionStatusWhenQueued = "active",
    };

    [Fact]
    public async Task A_queued_submission_survives_a_restart()
    {
        // The whole promise: build a pipeline at a rig site on Tuesday, submit it Thursday.
        await _outbox.EnqueueAsync(Entry("key-1"), Predictions("id,label\n1,A\n2,B\n"));

        var reopened = new SubmissionOutbox(_workspace);

        var entry = reopened.Pending().Should().ContainSingle().Subject;
        entry.Id.Should().Be("key-1");
        entry.LocalScore.Should().Be(0.87);

        await using var predictions = reopened.OpenPredictions("key-1");
        using var reader = new StreamReader(predictions!);
        (await reader.ReadToEndAsync()).Should().Contain("2,B");
    }

    [Fact]
    public async Task The_queue_is_capped_and_says_so_rather_than_dropping_the_oldest()
    {
        // Dropping the oldest would discard the submission that has been waiting longest — exactly the
        // loss this is built to prevent.
        for (var i = 0; i < SubmissionOutbox.MaxQueued; i++)
        {
            await _outbox.EnqueueAsync(Entry($"key-{i}"), Predictions());
        }

        var act = () => _outbox.EnqueueAsync(Entry("one-too-many"), Predictions());

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .And.Message.Should().Contain("waiting");
        _outbox.Pending().Should().HaveCount(SubmissionOutbox.MaxQueued);
    }

    [Fact]
    public async Task A_failure_backs_off_and_eventually_asks_for_a_person()
    {
        await _outbox.EnqueueAsync(Entry("flaky"), Predictions());

        for (var i = 1; i < SubmissionOutbox.MaxAttempts; i++)
        {
            _outbox.Failed("flaky", "timeout")!.State.Should().Be(OutboxState.Queued);
        }

        var parked = _outbox.Failed("flaky", "timeout")!;
        parked.State.Should().Be(OutboxState.NeedsAttention, "retrying forever against a wall helps nobody");
        parked.Attempts.Should().Be(SubmissionOutbox.MaxAttempts);
        _outbox.Pending().Should().BeEmpty();
        _outbox.List().Should().ContainSingle("it is parked, not deleted");
    }

    [Fact]
    public void The_backoff_grows_and_then_stops_growing()
    {
        // Capped because the network returning is the event that matters, and a schedule grown to hours
        // would sit idle through it.
        SubmissionOutbox.BackoffFor(1).Should().BeLessThan(SubmissionOutbox.BackoffFor(2));
        SubmissionOutbox.BackoffFor(2).Should().BeLessThan(SubmissionOutbox.BackoffFor(4));
        SubmissionOutbox.BackoffFor(20).Should().Be(SubmissionOutbox.BackoffFor(50));
        SubmissionOutbox.BackoffFor(50).Should().BeLessThanOrEqualTo(TimeSpan.FromMinutes(5));
    }

    [Fact]
    public async Task A_rejected_submission_is_kept_with_its_reason()
    {
        await _outbox.EnqueueAsync(Entry("late"), Predictions());

        _outbox.Rejected("late", "This competition concluded on 1 August.");

        var entry = _outbox.List().Should().ContainSingle().Subject;
        entry.State.Should().Be(OutboxState.Rejected);
        entry.LastError.Should().Contain("concluded");
        _outbox.OpenPredictions("late").Should().NotBeNull("the predictions are still theirs to use");
    }

    [Fact]
    public async Task Sending_clears_the_entry_and_its_file()
    {
        await _outbox.EnqueueAsync(Entry("done"), Predictions());

        _outbox.Sent("done", 0.91);

        _outbox.List().Should().BeEmpty();
        _outbox.OpenPredictions("done").Should().BeNull();
    }

    [Fact]
    public async Task One_unreadable_entry_does_not_hide_the_rest()
    {
        await _outbox.EnqueueAsync(Entry("good"), Predictions());
        await File.WriteAllTextAsync(Path.Combine(_outbox.FolderPath, "broken.json"), "{ not json");

        _outbox.Pending().Should().ContainSingle().Which.Id.Should().Be("good");
    }

    [Fact]
    public async Task An_id_cannot_write_outside_the_outbox()
    {
        // The id becomes a file name, and it is generated from a submission the app did not author.
        await _outbox.EnqueueAsync(Entry("../../escape"), Predictions());

        Directory.EnumerateFiles(_outbox.FolderPath).Should().NotBeEmpty();
        File.Exists(Path.Combine(_root, "escape.json")).Should().BeFalse();
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { /* best effort */ }
    }
}
