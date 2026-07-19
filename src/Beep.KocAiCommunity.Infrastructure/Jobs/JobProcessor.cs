using Beep.KocAiCommunity.Application.Jobs;
using Microsoft.Extensions.Logging;

namespace Beep.KocAiCommunity.Infrastructure.Jobs;

/// <summary>
/// Leases and runs a single job end-to-end: dispatch to the type's handler, renew the lease with a
/// heartbeat while it runs, observe cooperative cancellation, and record success/failure/retry.
/// Kept host-agnostic so it can be exercised directly in tests as well as from the Worker loop.
/// </summary>
public sealed class JobProcessor(IJobQueue queue, IEnumerable<IJobHandler> handlers, ILogger<JobProcessor> logger)
{
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan CancelPollInterval = TimeSpan.FromSeconds(5);

    private readonly Dictionary<string, IJobHandler> _handlers =
        handlers.ToDictionary(h => h.JobType, StringComparer.Ordinal);

    /// <summary>Process at most one job. Returns false when the queue had nothing runnable.</summary>
    public async Task<bool> ProcessOneAsync(string workerId, CancellationToken stoppingToken)
    {
        var job = await queue.TryLeaseAsync(workerId, LeaseDuration, stoppingToken);
        if (job is null)
        {
            return false;
        }

        if (!_handlers.TryGetValue(job.Type, out var handler))
        {
            await queue.AppendLogAsync(job.Id, "error", $"No handler registered for job type '{job.Type}'.", stoppingToken);
            // A missing handler is not retryable: exhaust attempts so it dead-letters instead of looping.
            for (var i = job.AttemptCount; i < job.MaxAttempts; i++)
            {
                await queue.FailAsync(job.Id, workerId, $"No handler for '{job.Type}'.", stoppingToken);
            }
            await queue.FailAsync(job.Id, workerId, $"No handler for '{job.Type}'.", stoppingToken);
            return true;
        }

        // Renew the lease and watch for cancellation in the background while the handler runs.
        using var handlerCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var keepAlive = KeepLeaseAliveAsync(job.Id, workerId, handlerCts, stoppingToken);

        var context = new JobExecutionContext(job.Id, job.PayloadJson,
            (severity, message) => queue.AppendLogAsync(job.Id, severity, message, stoppingToken));

        try
        {
            await handler.ExecuteAsync(context, handlerCts.Token);
            await queue.CompleteAsync(job.Id, workerId, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Distinguish a user cancel from a worker shutdown: only the former is terminal here.
            if (await queue.IsCancelRequestedAsync(job.Id, CancellationToken.None))
            {
                await queue.MarkCancelledAsync(job.Id, workerId, CancellationToken.None);
            }
            else
            {
                // Worker is shutting down mid-run: leave the job leased so it's reclaimed after restart.
                logger.LogInformation("Job {JobId} interrupted by shutdown; lease will expire for reclaim.", job.Id);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Job {JobId} handler failed.", job.Id);
            await queue.FailAsync(job.Id, workerId, ex.Message, CancellationToken.None);
        }
        finally
        {
            await handlerCts.CancelAsync();
            await keepAlive;
        }

        return true;
    }

    private async Task KeepLeaseAliveAsync(Guid jobId, string workerId, CancellationTokenSource handlerCts, CancellationToken stoppingToken)
    {
        try
        {
            while (!handlerCts.IsCancellationRequested)
            {
                await Task.Delay(HeartbeatInterval < CancelPollInterval ? HeartbeatInterval : CancelPollInterval, handlerCts.Token);
                await queue.HeartbeatAsync(jobId, workerId, LeaseDuration, stoppingToken);
                if (await queue.IsCancelRequestedAsync(jobId, stoppingToken))
                {
                    await handlerCts.CancelAsync(); // trip cooperative cancellation in the handler
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal: the handler finished or cancellation fired.
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Heartbeat loop ended for job {JobId}.", jobId);
        }
    }
}
