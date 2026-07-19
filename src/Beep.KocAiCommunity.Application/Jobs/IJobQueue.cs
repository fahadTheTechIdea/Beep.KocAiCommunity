using Beep.KocAiCommunity.Domain.Jobs;

namespace Beep.KocAiCommunity.Application.Jobs;

/// <summary>Well-known job type discriminators.</summary>
public static class JobTypes
{
    /// <summary>Train an ML.NET model from a stored dataset artifact.</summary>
    public const string ModelTrain = "model.train";

    /// <summary>Train with live trial-by-trial metric tracking into an experiment.</summary>
    public const string ExperimentTrain = "experiment.train";
}

/// <summary>
/// The durable, provider-portable job queue. Claiming is atomic (a job is leased to exactly one
/// worker); leases expire so a crashed worker's job is reclaimed; failures retry with backoff.
/// </summary>
public interface IJobQueue
{
    Task<Guid> EnqueueAsync(string type, string title, string payloadJson, string ownerUserId,
        int priority = 0, int maxAttempts = 3, CancellationToken ct = default);

    /// <summary>Atomically claim the next runnable job for <paramref name="workerId"/>, or null if none.</summary>
    Task<Job?> TryLeaseAsync(string workerId, TimeSpan lease, CancellationToken ct = default);

    /// <summary>Renew the lease for a job this worker still holds. No-op if the lease was lost.</summary>
    Task HeartbeatAsync(Guid jobId, string workerId, TimeSpan lease, CancellationToken ct = default);

    Task CompleteAsync(Guid jobId, string workerId, CancellationToken ct = default);

    /// <summary>Record a failure. Returns true if the job was rescheduled for retry, false if it dead-lettered.</summary>
    Task<bool> FailAsync(Guid jobId, string workerId, string error, CancellationToken ct = default);

    Task MarkCancelledAsync(Guid jobId, string workerId, CancellationToken ct = default);

    /// <summary>Request cooperative cancellation. A still-pending job is cancelled immediately.</summary>
    Task RequestCancelAsync(Guid jobId, CancellationToken ct = default);

    Task<bool> IsCancelRequestedAsync(Guid jobId, CancellationToken ct = default);

    Task AppendLogAsync(Guid jobId, string severity, string message, CancellationToken ct = default);

    Task<Job?> GetAsync(Guid jobId, CancellationToken ct = default);
    Task<IReadOnlyList<Job>> GetRecentForOwnerAsync(string ownerUserId, int take = 30, CancellationToken ct = default);
    Task<IReadOnlyList<JobLog>> GetLogsAsync(Guid jobId, CancellationToken ct = default);
    Task<IReadOnlyList<JobAttempt>> GetAttemptsAsync(Guid jobId, CancellationToken ct = default);
}
