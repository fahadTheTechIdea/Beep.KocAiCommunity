using System.Text;
using Beep.KocAiCommunity.Desktop.Local;
using FluentAssertions;
using Xunit;

namespace Beep.KocAiCommunity.UnitTests;

/// <summary>
/// Draining the queue when the network comes back.
/// <para>
/// A sync layer tested only online is not tested. These run it offline, half-offline, against a server
/// that refuses, and against one that keeps timing out — and the last one asserts the property that
/// matters: whatever order things happen in, every submission ends up either <b>sent exactly once</b>
/// or <b>explicitly rejected with a reason</b>. Never sent twice, never quietly gone.
/// </para>
/// </summary>
public sealed class SyncServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "koc-sync-" + Guid.NewGuid().ToString("N"));
    private readonly SubmissionOutbox _outbox;
    private readonly ConnectionState _connection = new();

    public SyncServiceTests()
    {
        var workspace = new LocalWorkspace { RootPath = _root };
        workspace.EnsureCreated();
        _outbox = new SubmissionOutbox(workspace);
    }

    /// <summary>A platform that behaves however the test needs — the only thing sync talks to.</summary>
    private sealed class FakeSender : ISubmissionSender
    {
        public bool Reachable { get; set; } = true;

        /// <summary>Keys the server actually accepted, so a double-send is visible.</summary>
        public List<string> Accepted { get; } = [];

        /// <summary>Set to throw on send — a refusal or a timeout, depending on the message.</summary>
        public Func<string, Exception?>? FailWith { get; set; }

        public Task<bool> IsReachableAsync(CancellationToken ct = default) => Task.FromResult(Reachable);

        public Task<double?> SendAsync(
            Guid competitionId, Stream predictions, string fileName, string idempotencyKey, CancellationToken ct = default)
        {
            if (FailWith?.Invoke(idempotencyKey) is { } failure)
            {
                throw failure;
            }

            // The server is idempotent, so a replayed key is not a second acceptance.
            if (!Accepted.Contains(idempotencyKey))
            {
                Accepted.Add(idempotencyKey);
            }

            return Task.FromResult<double?>(0.91);
        }
    }

    private SyncService Service(FakeSender sender) => new(sender, _outbox, _connection);

    private async Task QueueAsync(string id) =>
        await _outbox.EnqueueAsync(new QueuedSubmission
        {
            Id = id,
            CompetitionId = Guid.NewGuid(),
            CompetitionTitle = "ESP failure",
            QueuedUtc = DateTime.UtcNow,
            LocalScore = 0.87,
        }, new MemoryStream(Encoding.UTF8.GetBytes("id,label\n1,A\n")));

    [Fact]
    public async Task Reconnecting_drains_the_queue_without_anyone_asking()
    {
        await QueueAsync("a");
        await QueueAsync("b");
        var sender = new FakeSender();

        var outcome = await Service(sender).DrainAsync();

        outcome.Sent.Should().Be(2);
        outcome.Remaining.Should().Be(0);
        _outbox.List().Should().BeEmpty();
        _connection.Queued.Should().Be(0);
    }

    [Fact]
    public async Task Every_replay_carries_the_entry_id_as_its_key()
    {
        // This is the contract with the platform's idempotency. A key that changed between retries
        // would spend a second attempt against the daily quota.
        await QueueAsync("stable-key");
        var sender = new FakeSender { FailWith = _ => new TimeoutException("gateway timeout") };
        var service = Service(sender);

        // A clock that actually moves. A fixed offset would never get past the backoff, because the
        // last attempt is stamped from the same clock.
        var clock = DateTime.UtcNow;
        service.UtcNow = () => clock;

        await service.DrainAsync();
        clock = clock.AddMinutes(10);
        await service.DrainAsync();
        clock = clock.AddMinutes(10);

        sender.FailWith = null;
        await service.DrainAsync();

        sender.Accepted.Should().ContainSingle().Which.Should().Be("stable-key");
    }

    [Fact]
    public async Task A_closed_competition_is_rejected_with_the_reason_and_kept()
    {
        await QueueAsync("too-late");
        var sender = new FakeSender
        {
            FailWith = _ => new InvalidOperationException("This competition is not open for submissions."),
        };

        var outcome = await Service(sender).DrainAsync();

        outcome.Rejected.Should().Be(1);
        var entry = _outbox.List().Should().ContainSingle().Subject;
        entry.State.Should().Be(OutboxState.Rejected);
        entry.LastError.Should().Contain("not open");
    }

    [Fact]
    public async Task Quota_exhaustion_waits_rather_than_rejecting()
    {
        // The window reopens tomorrow. Rejecting would throw away work the server would have taken.
        await QueueAsync("tomorrow");
        var sender = new FakeSender
        {
            FailWith = _ => new InvalidOperationException("Daily submission quota (5) reached."),
        };

        var outcome = await Service(sender).DrainAsync();

        outcome.Rejected.Should().Be(0);
        outcome.Failed.Should().Be(1);
        _outbox.List().Should().ContainSingle().Which.State.Should().Be(OutboxState.Queued);
    }

    [Fact]
    public async Task A_recent_failure_is_skipped_rather_than_holding_up_the_queue()
    {
        await QueueAsync("sticky");
        await QueueAsync("fine");
        var sender = new FakeSender { FailWith = key => key == "sticky" ? new TimeoutException("no route") : null };
        var service = Service(sender);

        await service.DrainAsync();          // sticky fails, fine is sent
        var second = await service.DrainAsync();

        second.Failed.Should().Be(0, "sticky is inside its backoff and is skipped, not retried");
        sender.Accepted.Should().ContainSingle().Which.Should().Be("fine");
    }

    [Fact]
    public async Task Missing_predictions_are_rejected_rather_than_retried_forever()
    {
        await QueueAsync("orphan");
        File.Delete(Path.Combine(_outbox.FolderPath, "orphan.csv"));

        var outcome = await Service(new FakeSender()).DrainAsync();

        outcome.Rejected.Should().Be(1);
        _outbox.List().Should().ContainSingle().Which.LastError.Should().Contain("missing");
    }

    [Fact]
    public async Task Being_offline_leaves_the_queue_exactly_as_it_was()
    {
        await QueueAsync("waiting");
        var sender = new FakeSender { Reachable = false };
        var service = Service(sender);

        (await service.IsReachableAsync()).Should().BeFalse();
        _connection.Status.Should().Be(Connectivity.Offline);
        _outbox.Pending().Should().ContainSingle();
    }

    [Fact]
    public async Task Connectivity_is_our_api_answering_not_a_status_code()
    {
        var sender = new FakeSender { Reachable = true };
        var service = Service(sender);

        (await service.IsReachableAsync()).Should().BeTrue();
        _connection.Status.Should().Be(Connectivity.Online);
        _connection.LastCheckedUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task Whatever_order_it_happens_in_each_submission_is_sent_once_or_explicitly_rejected()
    {
        // The convergence property. A queue drained across an unreliable, intermittently reachable
        // network must never send the same submission twice and must never lose one — a sync layer
        // that only works when things go well is not a sync layer.
        var ids = Enumerable.Range(0, 12).Select(i => $"entry-{i}").ToList();
        foreach (var id in ids)
        {
            await QueueAsync(id);
        }

        var sender = new FakeSender();
        var service = Service(sender);
        var clock = DateTime.UtcNow;
        service.UtcNow = () => clock;

        // Deterministic but uneven: some entries refuse outright, some time out for a while, the
        // network drops in and out, and time moves in jumps past the backoff.
        var random = new Random(20260802);
        var doomed = ids.Where((_, i) => i % 5 == 0).ToHashSet();

        for (var round = 0; round < 25; round++)
        {
            sender.Reachable = random.Next(3) != 0;
            var flakyThisRound = random.Next(2) == 0;

            sender.FailWith = key =>
                doomed.Contains(key) ? new InvalidOperationException("This competition has concluded.")
                : flakyThisRound && random.Next(2) == 0 ? new TimeoutException("connection reset")
                : null;

            if (await service.IsReachableAsync())
            {
                await service.DrainAsync();
            }

            clock = clock.AddMinutes(10); // past any backoff
        }

        // Everything settled, and settled once.
        sender.Accepted.Should().OnlyHaveUniqueItems("no submission may be sent twice");
        sender.Accepted.Should().NotIntersectWith(doomed, "a refused submission was never accepted");

        var left = _outbox.List();
        left.Should().OnlyContain(e => e.State != OutboxState.Queued,
            "nothing should still be waiting after the network came back repeatedly");

        var accountedFor = sender.Accepted.Concat(left.Select(e => e.Id)).ToList();
        accountedFor.Should().BeEquivalentTo(ids, "every submission is either sent or still on disk with a reason");
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { /* best effort */ }
    }
}
