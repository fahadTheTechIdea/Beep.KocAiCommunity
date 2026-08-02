namespace Beep.KocAiCommunity.Desktop.Local;

/// <summary>What one drain attempt did, so the caller can report and the tests can assert.</summary>
public sealed record SyncOutcome(int Sent, int Rejected, int Failed, int Remaining);

/// <summary>
/// Sends queued submissions when the network comes back.
/// <para>
/// Every replay carries the queued submission's own id as the idempotency key, which is the whole
/// reason the platform grew one: submissions are quota-limited, and a client that resends a request it
/// never saw the answer to would otherwise spend a participant's attempt twice.
/// </para>
/// <para>
/// The three endings are kept apart deliberately. <b>Sent</b> clears the entry. <b>Rejected</b> — the
/// competition closed, or is no longer visible — keeps it with the reason, because the person did the
/// work. <b>Failed</b> backs off and tries again, and after five attempts stops and asks for a person
/// rather than spinning against a wall.
/// </para>
/// </summary>
public sealed class SyncService(
    ISubmissionSender sender,
    SubmissionOutbox outbox,
    ConnectionState connection)
{
    /// <summary>How often connectivity is re-checked. The indicator should reflect reality inside this.</summary>
    public static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(20);

    /// <summary>Replaced in tests; there is no other way to make a wall clock move on demand.</summary>
    public Func<DateTime> UtcNow { get; set; } = () => DateTime.UtcNow;

    /// <summary>Raised when an entry finishes, so the UI can say so without polling.</summary>
    public event Action<QueuedSubmission, string>? EntryFinished;

    /// <summary>Asks the platform whether it is there, and records the answer for the whole app.</summary>
    public async Task<bool> IsReachableAsync(CancellationToken ct = default)
    {
        var reachable = await sender.IsReachableAsync(ct);
        connection.Status = reachable ? Connectivity.Online : Connectivity.Offline;
        connection.LastCheckedUtc = UtcNow();
        return reachable;
    }

    /// <summary>
    /// Sends what it can, oldest first. Safe to call repeatedly — a replay costs nothing on the server.
    /// </summary>
    public async Task<SyncOutcome> DrainAsync(CancellationToken ct = default)
    {
        var sent = 0;
        var rejected = 0;
        var failed = 0;

        foreach (var entry in outbox.Pending())
        {
            ct.ThrowIfCancellationRequested();

            // Back off without blocking: an entry that failed recently is skipped this pass rather than
            // slept on, so one bad submission cannot hold up the rest of the queue. Counted from the
            // last attempt — counting from when it was queued would make an old entry retry instantly
            // however often it has just failed.
            if (entry is { Attempts: > 0, LastAttemptUtc: { } lastTried }
                && UtcNow() - lastTried < SubmissionOutbox.BackoffFor(entry.Attempts))
            {
                continue;
            }

            var result = await SendAsync(entry, ct);
            switch (result)
            {
                case SendResult.Sent:
                    sent++;
                    break;
                case SendResult.Rejected:
                    rejected++;
                    break;
                default:
                    failed++;
                    break;
            }
        }

        connection.Queued = outbox.Pending().Count;
        return new SyncOutcome(sent, rejected, failed, connection.Queued);
    }

    private enum SendResult { Sent, Rejected, Failed }

    private async Task<SendResult> SendAsync(QueuedSubmission entry, CancellationToken ct)
    {
        await using var predictions = outbox.OpenPredictions(entry.Id);
        if (predictions is null)
        {
            // The predictions are gone — deleted by hand, or a half-written enqueue. There is nothing
            // to send and never will be, so say so rather than retrying forever.
            var orphan = outbox.Rejected(entry.Id, "The predictions file for this submission is missing.");
            Finished(orphan, "rejected");
            return SendResult.Rejected;
        }

        try
        {
            var score = await sender.SendAsync(entry.CompetitionId, predictions, $"{entry.Id}.csv", entry.Id, ct);

            outbox.Sent(entry.Id, score);
            Finished(entry with { ServerScore = score }, "sent");
            return SendResult.Sent;
        }
        catch (Exception ex) when (IsRefusal(ex))
        {
            var refused = outbox.Rejected(entry.Id, ex.Message);
            Finished(refused, "rejected");
            return SendResult.Rejected;
        }
        catch (Exception ex)
        {
            var retry = outbox.Failed(entry.Id, ex.Message, UtcNow());
            if (retry?.State == OutboxState.NeedsAttention)
            {
                Finished(retry, "needs attention");
            }

            return SendResult.Failed;
        }
    }

    /// <summary>
    /// Whether the server said no, as opposed to not answering.
    /// <para>
    /// The distinction decides whether an entry is retried forever or set aside with a reason. A closed
    /// competition will never accept this submission however many times it is offered; a timeout might
    /// accept it in a minute. Quota exhaustion is deliberately <em>not</em> a refusal — the window
    /// reopens tomorrow, and rejecting it would throw away work that would have been accepted.
    /// </para>
    /// </summary>
    private static bool IsRefusal(Exception ex)
    {
        var message = ex.Message;

        if (message.Contains("quota", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return message.Contains("not open for submissions", StringComparison.OrdinalIgnoreCase)
            || message.Contains("concluded", StringComparison.OrdinalIgnoreCase)
            || message.Contains("not visible", StringComparison.OrdinalIgnoreCase)
            || message.Contains("not found", StringComparison.OrdinalIgnoreCase);
    }

    private void Finished(QueuedSubmission? entry, string what)
    {
        if (entry is not null)
        {
            EntryFinished?.Invoke(entry, what);
        }
    }
}
