using Beep.KocAiCommunity.Application.Jobs;
using Beep.KocAiCommunity.Application.RealTime;
using Beep.KocAiCommunity.Domain.Jobs;
using Beep.KocAiCommunity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Beep.KocAiCommunity.Infrastructure.Jobs;

/// <summary>
/// EF-backed durable job queue. Claiming uses a guarded <c>ExecuteUpdate</c> — a single conditional
/// UPDATE that only the winning worker's row-write satisfies — so a job is leased to exactly one
/// worker on both SQLite and SQL Server without provider-specific locking hints. Expired leases are
/// reclaimable, so a crashed worker's job resumes.
/// </summary>
public sealed class EfJobQueue(KocDbContext db, IOutboxWriter outbox) : IJobQueue
{
    public async Task<Guid> EnqueueAsync(string type, string title, string payloadJson, string ownerUserId,
        int priority = 0, int maxAttempts = 3, CancellationToken ct = default)
    {
        var job = new Job
        {
            Type = type,
            Title = string.IsNullOrWhiteSpace(title) ? type : title,
            PayloadJson = string.IsNullOrWhiteSpace(payloadJson) ? "{}" : payloadJson,
            OwnerUserId = ownerUserId,
            Status = JobStatus.Pending,
            Priority = priority,
            MaxAttempts = maxAttempts <= 0 ? 3 : maxAttempts,
            CreatedByUserId = ownerUserId,
            CreatedUtc = DateTime.UtcNow,
        };
        db.Set<Job>().Add(job);
        await db.SaveChangesAsync(ct);
        return job.Id;
    }

    public async Task<Job?> TryLeaseAsync(string workerId, TimeSpan lease, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var leaseExpiry = now.Add(lease);

        var candidateIds = await db.Set<Job>().AsNoTracking()
            .Where(j => !j.IsDeleted && !j.CancelRequested
                && (j.Status == JobStatus.Pending || j.Status == JobStatus.Running)
                && (j.NextAttemptUtc == null || j.NextAttemptUtc <= now)
                && (j.LeaseExpiresUtc == null || j.LeaseExpiresUtc <= now))
            .OrderByDescending(j => j.Priority).ThenBy(j => j.CreatedUtc)
            .Select(j => j.Id)
            .Take(10)
            .ToListAsync(ct);

        foreach (var id in candidateIds)
        {
            // Only the worker whose UPDATE matches the still-unclaimed row wins the lease.
            var affected = await db.Set<Job>()
                .Where(j => j.Id == id && !j.CancelRequested
                    && (j.Status == JobStatus.Pending || j.Status == JobStatus.Running)
                    && (j.LeaseExpiresUtc == null || j.LeaseExpiresUtc <= now))
                .ExecuteUpdateAsync(s => s
                    .SetProperty(j => j.Status, JobStatus.Running)
                    .SetProperty(j => j.LeaseOwnerId, workerId)
                    .SetProperty(j => j.LeaseExpiresUtc, leaseExpiry)
                    .SetProperty(j => j.LastHeartbeatUtc, now)
                    .SetProperty(j => j.StartedUtc, j => j.StartedUtc == null ? now : j.StartedUtc)
                    .SetProperty(j => j.AttemptCount, j => j.AttemptCount + 1), ct);

            if (affected != 1)
            {
                continue; // another worker claimed it first
            }

            var job = await db.Set<Job>().AsNoTracking().FirstAsync(j => j.Id == id, ct);

            db.Set<JobAttempt>().Add(new JobAttempt
            {
                JobId = id,
                AttemptNumber = job.AttemptCount,
                StartedUtc = now,
                Status = JobStatus.Running,
                WorkerId = workerId,
                CreatedUtc = now,
            });
            AddLog(id, "info", $"Leased by {workerId} (attempt {job.AttemptCount}/{job.MaxAttempts}).", now);
            await outbox.EnqueueAsync(new RunProgressEvent(id, JobStatus.Running, "Run started."), ct);
            await db.SaveChangesAsync(ct);
            return job;
        }

        return null;
    }

    public Task HeartbeatAsync(Guid jobId, string workerId, TimeSpan lease, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        return db.Set<Job>()
            .Where(j => j.Id == jobId && j.LeaseOwnerId == workerId && j.Status == JobStatus.Running)
            .ExecuteUpdateAsync(s => s
                .SetProperty(j => j.LeaseExpiresUtc, now.Add(lease))
                .SetProperty(j => j.LastHeartbeatUtc, now), ct);
    }

    public async Task CompleteAsync(Guid jobId, string workerId, CancellationToken ct = default)
    {
        var job = await db.Set<Job>().AsNoTracking().FirstOrDefaultAsync(j => j.Id == jobId, ct);
        if (job is null || JobStatus.IsTerminal(job.Status))
        {
            return;
        }

        var now = DateTime.UtcNow;
        // ExecuteUpdate (not a tracked mutation) so the status write is unaffected by the identity map.
        await db.Set<Job>().Where(j => j.Id == jobId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(j => j.Status, JobStatus.Succeeded)
                .SetProperty(j => j.CompletedUtc, now)
                .SetProperty(j => j.LeaseOwnerId, (string?)null)
                .SetProperty(j => j.LeaseExpiresUtc, (DateTime?)null), ct);

        await CloseAttemptAsync(jobId, JobStatus.Succeeded, null, now, ct);
        AddLog(jobId, "info", "Run completed successfully.", now);
        await outbox.EnqueueAsync(new RunProgressEvent(jobId, JobStatus.Succeeded, "Run completed."), ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task<bool> FailAsync(Guid jobId, string workerId, string error, CancellationToken ct = default)
    {
        var job = await db.Set<Job>().AsNoTracking().FirstOrDefaultAsync(j => j.Id == jobId, ct);
        if (job is null || JobStatus.IsTerminal(job.Status))
        {
            return false;
        }

        var now = DateTime.UtcNow;
        var lastError = Truncate(error, 2000);
        await CloseAttemptAsync(jobId, JobStatus.Failed, error, now, ct);

        var willRetry = job.AttemptCount < job.MaxAttempts && !job.CancelRequested;
        if (willRetry)
        {
            var delay = RetryPolicy.BackoffFor(job.AttemptCount) + TimeSpan.FromMilliseconds(Random.Shared.Next(0, 2000));
            var nextAttempt = now.Add(delay);
            await db.Set<Job>().Where(j => j.Id == jobId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(j => j.Status, JobStatus.Pending)
                    .SetProperty(j => j.LastError, lastError)
                    .SetProperty(j => j.NextAttemptUtc, nextAttempt)
                    .SetProperty(j => j.LeaseOwnerId, (string?)null)
                    .SetProperty(j => j.LeaseExpiresUtc, (DateTime?)null), ct);
            AddLog(jobId, "warning", $"Attempt {job.AttemptCount} failed; retrying in {delay.TotalSeconds:0}s. {Truncate(error, 400)}", now);
            await outbox.EnqueueAsync(new RunProgressEvent(jobId, JobStatus.Pending, "Retry scheduled."), ct);
        }
        else
        {
            var terminal = job.CancelRequested ? JobStatus.Cancelled : JobStatus.DeadLetter;
            await db.Set<Job>().Where(j => j.Id == jobId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(j => j.Status, terminal)
                    .SetProperty(j => j.LastError, lastError)
                    .SetProperty(j => j.CompletedUtc, now)
                    .SetProperty(j => j.LeaseOwnerId, (string?)null)
                    .SetProperty(j => j.LeaseExpiresUtc, (DateTime?)null), ct);
            AddLog(jobId, "error", $"Run failed permanently after {job.AttemptCount} attempt(s). {Truncate(error, 400)}", now);
            await outbox.EnqueueAsync(new RunProgressEvent(jobId, terminal, "Run failed."), ct);
        }

        await db.SaveChangesAsync(ct);
        return willRetry;
    }

    public async Task MarkCancelledAsync(Guid jobId, string workerId, CancellationToken ct = default)
    {
        var job = await db.Set<Job>().AsNoTracking().FirstOrDefaultAsync(j => j.Id == jobId, ct);
        if (job is null || JobStatus.IsTerminal(job.Status))
        {
            return;
        }

        var now = DateTime.UtcNow;
        await db.Set<Job>().Where(j => j.Id == jobId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(j => j.Status, JobStatus.Cancelled)
                .SetProperty(j => j.CompletedUtc, now)
                .SetProperty(j => j.LeaseOwnerId, (string?)null)
                .SetProperty(j => j.LeaseExpiresUtc, (DateTime?)null), ct);

        await CloseAttemptAsync(jobId, JobStatus.Cancelled, "cancelled", now, ct);
        AddLog(jobId, "warning", "Run cancelled.", now);
        await outbox.EnqueueAsync(new RunProgressEvent(jobId, JobStatus.Cancelled, "Run cancelled."), ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task RequestCancelAsync(Guid jobId, CancellationToken ct = default)
    {
        var job = await db.Set<Job>().AsNoTracking().FirstOrDefaultAsync(j => j.Id == jobId, ct);
        if (job is null || JobStatus.IsTerminal(job.Status))
        {
            return;
        }

        var now = DateTime.UtcNow;

        // A job that hasn't been leased yet can be cancelled outright; a running one is flagged for
        // the runner to stop cooperatively.
        if (job.Status == JobStatus.Pending)
        {
            await db.Set<Job>().Where(j => j.Id == jobId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(j => j.CancelRequested, true)
                    .SetProperty(j => j.Status, JobStatus.Cancelled)
                    .SetProperty(j => j.CompletedUtc, now)
                    .SetProperty(j => j.LeaseOwnerId, (string?)null)
                    .SetProperty(j => j.LeaseExpiresUtc, (DateTime?)null), ct);
            AddLog(jobId, "warning", "Run cancelled before it started.", now);
            await outbox.EnqueueAsync(new RunProgressEvent(jobId, JobStatus.Cancelled, "Run cancelled."), ct);
        }
        else
        {
            await db.Set<Job>().Where(j => j.Id == jobId)
                .ExecuteUpdateAsync(s => s.SetProperty(j => j.CancelRequested, true), ct);
            AddLog(jobId, "info", "Cancellation requested; stopping soon.", now);
        }

        await db.SaveChangesAsync(ct);
    }

    public Task<bool> IsCancelRequestedAsync(Guid jobId, CancellationToken ct = default) =>
        db.Set<Job>().AsNoTracking().Where(j => j.Id == jobId).Select(j => j.CancelRequested).FirstOrDefaultAsync(ct);

    public async Task AppendLogAsync(Guid jobId, string severity, string message, CancellationToken ct = default)
    {
        AddLog(jobId, severity, message, DateTime.UtcNow);
        await outbox.EnqueueAsync(new RunProgressEvent(jobId, JobStatus.Running, message), ct);
        await db.SaveChangesAsync(ct);
    }

    public Task<Job?> GetAsync(Guid jobId, CancellationToken ct = default) =>
        db.Set<Job>().AsNoTracking().FirstOrDefaultAsync(j => j.Id == jobId, ct);

    public async Task<IReadOnlyList<Job>> GetRecentForOwnerAsync(string ownerUserId, int take = 30, CancellationToken ct = default) =>
        await db.Set<Job>().AsNoTracking()
            .Where(j => j.OwnerUserId == ownerUserId)
            .OrderByDescending(j => j.CreatedUtc)
            .Take(Math.Clamp(take, 1, 100))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<JobLog>> GetLogsAsync(Guid jobId, CancellationToken ct = default) =>
        await db.Set<JobLog>().AsNoTracking()
            .Where(l => l.JobId == jobId)
            .OrderBy(l => l.LoggedUtc).ThenBy(l => l.Id)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<JobAttempt>> GetAttemptsAsync(Guid jobId, CancellationToken ct = default) =>
        await db.Set<JobAttempt>().AsNoTracking()
            .Where(a => a.JobId == jobId)
            .OrderBy(a => a.AttemptNumber)
            .ToListAsync(ct);

    private void AddLog(Guid jobId, string severity, string message, DateTime now) =>
        db.Set<JobLog>().Add(new JobLog
        {
            JobId = jobId,
            LoggedUtc = now,
            Severity = severity,
            Message = Truncate(message, 2000),
            CreatedUtc = now,
        });

    private async Task CloseAttemptAsync(Guid jobId, string status, string? error, DateTime now, CancellationToken ct)
    {
        var attempt = await db.Set<JobAttempt>()
            .Where(a => a.JobId == jobId && a.CompletedUtc == null)
            .OrderByDescending(a => a.AttemptNumber)
            .FirstOrDefaultAsync(ct);

        if (attempt is not null)
        {
            attempt.Status = status;
            attempt.CompletedUtc = now;
            attempt.Error = error is null ? null : Truncate(error, 2000);
        }
    }

    private static string Truncate(string value, int max) =>
        string.IsNullOrEmpty(value) || value.Length <= max ? value : value[..max];
}
