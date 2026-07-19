using Beep.KocAiCommunity.Application.Jobs;
using Beep.KocAiCommunity.Domain.Jobs;
using Beep.KocAiCommunity.Infrastructure.Jobs;
using Beep.KocAiCommunity.Infrastructure.Messaging;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Beep.KocAiCommunity.UnitTests;

public class JobProcessorTests
{
    // A handler whose behaviour the test supplies.
    private sealed class FakeHandler(string type, Func<JobExecutionContext, CancellationToken, Task> body) : IJobHandler
    {
        public string JobType => type;
        public Task ExecuteAsync(JobExecutionContext context, CancellationToken ct) => body(context, ct);
    }

    private static (EfJobQueue queue, JobProcessor processor) Build(OrgTestContext ctx, IJobHandler handler)
    {
        var queue = new EfJobQueue(ctx.Db, new OutboxWriter(ctx.Db));
        var processor = new JobProcessor(queue, [handler], NullLogger<JobProcessor>.Instance);
        return (queue, processor);
    }

    [Fact]
    public async Task Returns_false_when_the_queue_is_empty()
    {
        using var ctx = new OrgTestContext();
        var (_, processor) = Build(ctx, new FakeHandler("noop", (_, _) => Task.CompletedTask));

        (await processor.ProcessOneAsync("worker-A", CancellationToken.None)).Should().BeFalse();
    }

    [Fact]
    public async Task A_successful_handler_completes_the_job()
    {
        using var ctx = new OrgTestContext();
        var handler = new FakeHandler("test.ok", async (c, _) => await c.LogAsync("working…"));
        var (queue, processor) = Build(ctx, handler);
        var id = await queue.EnqueueAsync("test.ok", "OK", "{}", "emp1");

        var ran = await processor.ProcessOneAsync("worker-A", CancellationToken.None);

        ran.Should().BeTrue();
        (await queue.GetAsync(id))!.Status.Should().Be(JobStatus.Succeeded);
        (await queue.GetLogsAsync(id)).Should().Contain(l => l.Message == "working…");
    }

    [Fact]
    public async Task A_throwing_handler_dead_letters_when_out_of_attempts()
    {
        using var ctx = new OrgTestContext();
        var handler = new FakeHandler("test.boom", (_, _) => throw new InvalidOperationException("kaboom"));
        var (queue, processor) = Build(ctx, handler);
        var id = await queue.EnqueueAsync("test.boom", "Boom", "{}", "emp1", maxAttempts: 1);

        await processor.ProcessOneAsync("worker-A", CancellationToken.None);

        var job = await queue.GetAsync(id);
        job!.Status.Should().Be(JobStatus.DeadLetter);
        job.LastError.Should().Contain("kaboom");
    }

    [Fact]
    public async Task A_cancelled_run_ends_in_the_cancelled_state()
    {
        using var ctx = new OrgTestContext();
        // The handler requests its own cancellation then observes the token — mirrors a user cancel.
        EfJobQueue? q = null;
        var handler = new FakeHandler("test.cancel", async (c, ct) =>
        {
            await q!.RequestCancelAsync(c.JobId, CancellationToken.None);
            ct.ThrowIfCancellationRequested();
            throw new OperationCanceledException();
        });
        var (queue, processor) = Build(ctx, handler);
        q = queue;
        var id = await queue.EnqueueAsync("test.cancel", "Cancel", "{}", "emp1");

        await processor.ProcessOneAsync("worker-A", CancellationToken.None);

        (await queue.GetAsync(id))!.Status.Should().Be(JobStatus.Cancelled);
    }
}
