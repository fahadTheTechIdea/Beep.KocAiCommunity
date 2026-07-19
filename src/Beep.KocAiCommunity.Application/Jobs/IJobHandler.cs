namespace Beep.KocAiCommunity.Application.Jobs;

/// <summary>Context handed to a job handler: the payload plus a progress-logging channel.</summary>
public sealed class JobExecutionContext(Guid jobId, string payloadJson, Func<string, string, Task> logAsync)
{
    public Guid JobId { get; } = jobId;
    public string PayloadJson { get; } = payloadJson;

    /// <summary>Emit a progress line (streamed to the run detail page and persisted).</summary>
    public Task LogAsync(string message, string severity = "info") => logAsync(severity, message);
}

/// <summary>Executes one job type. Handlers must honour the cancellation token cooperatively.</summary>
public interface IJobHandler
{
    /// <summary>The <see cref="JobTypes"/> value this handler runs.</summary>
    string JobType { get; }

    Task ExecuteAsync(JobExecutionContext context, CancellationToken ct);
}
