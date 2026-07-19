using Beep.KocAiCommunity.Application.Jobs;
using Beep.KocAiCommunity.Domain.Jobs;
using Beep.KocAiCommunity.Infrastructure.Jobs;
using Beep.KocAiCommunity.Infrastructure.Messaging;
using FluentAssertions;
using Xunit;

namespace Beep.KocAiCommunity.UnitTests;

public class JobQueueTests
{
    private static EfJobQueue Queue(OrgTestContext ctx) => new(ctx.Db, new OutboxWriter(ctx.Db));

    [Fact]
    public async Task Enqueue_then_lease_marks_the_job_running_and_owned()
    {
        using var ctx = new OrgTestContext();
        var q = Queue(ctx);
        var id = await q.EnqueueAsync(JobTypes.ModelTrain, "Train pumps", "{}", "emp1");

        var job = await q.TryLeaseAsync("worker-A", TimeSpan.FromMinutes(1));

        job.Should().NotBeNull();
        job!.Id.Should().Be(id);
        job.Status.Should().Be(JobStatus.Running);
        job.LeaseOwnerId.Should().Be("worker-A");
        job.AttemptCount.Should().Be(1);
    }

    [Fact]
    public async Task A_leased_job_cannot_be_claimed_by_a_second_worker()
    {
        using var ctx = new OrgTestContext();
        var q = Queue(ctx);
        await q.EnqueueAsync(JobTypes.ModelTrain, "Train", "{}", "emp1");

        var first = await q.TryLeaseAsync("worker-A", TimeSpan.FromMinutes(1));
        var second = await q.TryLeaseAsync("worker-B", TimeSpan.FromMinutes(1));

        first.Should().NotBeNull();
        second.Should().BeNull(); // no duplicate claim
    }

    [Fact]
    public async Task An_expired_lease_is_reclaimable_after_a_worker_crash()
    {
        using var ctx = new OrgTestContext();
        var q = Queue(ctx);
        await q.EnqueueAsync(JobTypes.ModelTrain, "Train", "{}", "emp1");

        // Lease with an already-expired duration to simulate a worker that died mid-run.
        var first = await q.TryLeaseAsync("worker-A", TimeSpan.FromSeconds(-30));
        var reclaimed = await q.TryLeaseAsync("worker-B", TimeSpan.FromMinutes(1));

        first.Should().NotBeNull();
        reclaimed.Should().NotBeNull();
        reclaimed!.LeaseOwnerId.Should().Be("worker-B");
        reclaimed.AttemptCount.Should().Be(2); // survived the "restart"
    }

    [Fact]
    public async Task Complete_moves_the_job_to_succeeded()
    {
        using var ctx = new OrgTestContext();
        var q = Queue(ctx);
        var id = await q.EnqueueAsync(JobTypes.ModelTrain, "Train", "{}", "emp1");
        await q.TryLeaseAsync("worker-A", TimeSpan.FromMinutes(1));

        await q.CompleteAsync(id, "worker-A");

        (await q.GetAsync(id))!.Status.Should().Be(JobStatus.Succeeded);
    }

    [Fact]
    public async Task Fail_reschedules_for_retry_when_attempts_remain()
    {
        using var ctx = new OrgTestContext();
        var q = Queue(ctx);
        var id = await q.EnqueueAsync(JobTypes.ModelTrain, "Train", "{}", "emp1", maxAttempts: 3);
        await q.TryLeaseAsync("worker-A", TimeSpan.FromMinutes(1));

        var willRetry = await q.FailAsync(id, "worker-A", "boom");

        willRetry.Should().BeTrue();
        var job = await q.GetAsync(id);
        job!.Status.Should().Be(JobStatus.Pending);
        job.NextAttemptUtc.Should().NotBeNull();
        job.NextAttemptUtc!.Value.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public async Task Fail_deadletters_when_attempts_are_exhausted()
    {
        using var ctx = new OrgTestContext();
        var q = Queue(ctx);
        var id = await q.EnqueueAsync(JobTypes.ModelTrain, "Train", "{}", "emp1", maxAttempts: 1);
        await q.TryLeaseAsync("worker-A", TimeSpan.FromMinutes(1));

        var willRetry = await q.FailAsync(id, "worker-A", "boom");

        willRetry.Should().BeFalse();
        (await q.GetAsync(id))!.Status.Should().Be(JobStatus.DeadLetter);
    }

    [Fact]
    public async Task Cancelling_a_pending_job_cancels_it_immediately()
    {
        using var ctx = new OrgTestContext();
        var q = Queue(ctx);
        var id = await q.EnqueueAsync(JobTypes.ModelTrain, "Train", "{}", "emp1");

        await q.RequestCancelAsync(id);

        (await q.GetAsync(id))!.Status.Should().Be(JobStatus.Cancelled);
    }

    [Fact]
    public async Task Cancelling_a_running_job_flags_it_without_a_forced_stop()
    {
        using var ctx = new OrgTestContext();
        var q = Queue(ctx);
        var id = await q.EnqueueAsync(JobTypes.ModelTrain, "Train", "{}", "emp1");
        await q.TryLeaseAsync("worker-A", TimeSpan.FromMinutes(1));

        await q.RequestCancelAsync(id);

        (await q.IsCancelRequestedAsync(id)).Should().BeTrue();
        (await q.GetAsync(id))!.Status.Should().Be(JobStatus.Running); // runner stops it cooperatively
    }
}
