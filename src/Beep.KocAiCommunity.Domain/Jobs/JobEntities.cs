using Beep.KocAiCommunity.Domain.Common;

namespace Beep.KocAiCommunity.Domain.Jobs;

/// <summary>
/// A durable unit of background work ("run"). Persisted so it survives Worker restarts; claimed by a
/// Worker under a heartbeat lease; retried with backoff on failure; cancellable cooperatively.
/// </summary>
public class Job : AuditableEntity
{
    public string Type { get; set; } = default!;              // "model.train", …
    public string Title { get; set; } = default!;             // human label for the run list
    public string PayloadJson { get; set; } = "{}";
    public string OwnerUserId { get; set; } = default!;

    public string Status { get; set; } = JobStatus.Pending;   // see JobStatus
    public int Priority { get; set; }
    public int AttemptCount { get; set; }
    public int MaxAttempts { get; set; } = 3;

    public string? LeaseOwnerId { get; set; }
    public DateTime? LeaseExpiresUtc { get; set; }
    public DateTime? LastHeartbeatUtc { get; set; }
    public DateTime? NextAttemptUtc { get; set; }             // earliest time a retry may be leased

    public DateTime? StartedUtc { get; set; }
    public DateTime? CompletedUtc { get; set; }
    public string? LastError { get; set; }

    /// <summary>Cooperative cancel flag; the runner observes it and stops the handler.</summary>
    public bool CancelRequested { get; set; }
}

/// <summary>Stable Job.Status values.</summary>
public static class JobStatus
{
    public const string Pending = "pending";
    public const string Running = "running";
    public const string Succeeded = "succeeded";
    public const string Failed = "failed";
    public const string Cancelled = "cancelled";
    public const string DeadLetter = "deadletter";

    /// <summary>A job in a terminal state does no more work.</summary>
    public static bool IsTerminal(string status) =>
        status is Succeeded or Failed or Cancelled or DeadLetter;
}

/// <summary>One execution attempt of a <see cref="Job"/>.</summary>
public class JobAttempt : AuditableEntity
{
    public Guid JobId { get; set; }
    public int AttemptNumber { get; set; }
    public DateTime StartedUtc { get; set; }
    public DateTime? CompletedUtc { get; set; }
    public string Status { get; set; } = default!;           // running, succeeded, failed, cancelled
    public string? Error { get; set; }
    public string? WorkerId { get; set; }
}

/// <summary>A timestamped log line emitted while a job runs (streamed to the run detail page).</summary>
public class JobLog : AuditableEntity
{
    public Guid JobId { get; set; }
    public DateTime LoggedUtc { get; set; }
    public string Severity { get; set; } = "info";           // info, warning, error
    public string Message { get; set; } = default!;
}
