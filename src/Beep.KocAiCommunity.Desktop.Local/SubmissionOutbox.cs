using System.Text.Json;

namespace Beep.KocAiCommunity.Desktop.Local;

/// <summary>Where a queued submission has got to.</summary>
public enum OutboxState
{
    /// <summary>Waiting for the network.</summary>
    Queued,

    /// <summary>Tried and failed enough times that it wants a person to look at it.</summary>
    NeedsAttention,

    /// <summary>The server refused it, and said why. Kept, never deleted.</summary>
    Rejected,
}

/// <summary>
/// A submission made while the network was not there.
/// <para>
/// The snapshot fields are the point of writing this down at all. On replay we need to know whether the
/// competition is still the thing it was when the work was done — a submission that arrives after the
/// competition concluded has to be told what happened, not silently dropped or silently accepted.
/// </para>
/// </summary>
public sealed record QueuedSubmission
{
    /// <summary>The idempotency key, and the file name. Constant across every retry — that is its job.</summary>
    public required string Id { get; init; }

    public required Guid CompetitionId { get; init; }

    /// <summary>Kept so the queue can be shown without a network call.</summary>
    public required string CompetitionTitle { get; init; }

    public required DateTime QueuedUtc { get; init; }

    /// <summary>Which workflow produced it, for lineage.</summary>
    public Guid? WorkflowId { get; init; }

    /// <summary>
    /// What was measured locally. Shown beside the server's score when it arrives — they *should*
    /// differ, and that gap is the thing that teaches overfitting.
    /// </summary>
    public double? LocalScore { get; init; }

    /// <summary>The competition's state when this was queued, so a change is detectable on replay.</summary>
    public string? CompetitionStatusWhenQueued { get; init; }

    public DateTime? RevealUtcWhenQueued { get; init; }

    public int Attempts { get; init; }

    /// <summary>When it was last tried. The backoff counts from here, not from when it was queued.</summary>
    public DateTime? LastAttemptUtc { get; init; }

    public string? LastError { get; init; }

    public OutboxState State { get; init; } = OutboxState.Queued;

    /// <summary>The score the server gave it, once it landed.</summary>
    public double? ServerScore { get; init; }
}

/// <summary>
/// Submissions waiting for the network, on disk so they survive a restart.
/// <para>
/// The rule this is built around: <b>nothing is ever silently discarded.</b> An engineer at a rig site
/// who builds a good pipeline on Tuesday should not have to rebuild it on Thursday, and a submission
/// the server refuses is kept with the reason rather than deleted — they did the work either way.
/// </para>
/// </summary>
public sealed class SubmissionOutbox(LocalWorkspace workspace)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    /// <summary>
    /// How many may wait at once. A cap so a laptop that has been off the network for weeks does not
    /// fill its disk with predictions; beyond it the app refuses politely and says why, rather than
    /// accepting work it will not send.
    /// </summary>
    public const int MaxQueued = 50;

    /// <summary>Attempts before it stops retrying and asks for a person.</summary>
    public const int MaxAttempts = 5;

    private readonly Lock _gate = new();

    public string FolderPath => Path.Combine(workspace.RootPath, "outbox");

    /// <summary>Oldest first — the order they should be sent in.</summary>
    public IReadOnlyList<QueuedSubmission> List()
    {
        lock (_gate)
        {
            Directory.CreateDirectory(FolderPath);
            return [.. Directory.EnumerateFiles(FolderPath, "*.json")
                .Select(Read)
                .OfType<QueuedSubmission>()
                .OrderBy(e => e.QueuedUtc)];
        }
    }

    /// <summary>Those still worth sending, oldest first.</summary>
    public IReadOnlyList<QueuedSubmission> Pending() =>
        [.. List().Where(e => e.State == OutboxState.Queued)];

    public QueuedSubmission? Get(string id) => Read(PathFor(id));

    /// <summary>The predictions that go with a queued submission.</summary>
    public Stream? OpenPredictions(string id)
    {
        var path = Path.Combine(FolderPath, $"{Safe(id)}.csv");
        return File.Exists(path) ? File.OpenRead(path) : null;
    }

    /// <summary>
    /// Queues a submission and its predictions.
    /// <para>
    /// Throws when the queue is full rather than dropping the oldest: the oldest is the one that has
    /// been waiting longest, and quietly discarding it would be exactly the loss this exists to prevent.
    /// </para>
    /// </summary>
    public async Task<QueuedSubmission> EnqueueAsync(
        QueuedSubmission entry, Stream predictions, CancellationToken ct = default)
    {
        lock (_gate)
        {
            Directory.CreateDirectory(FolderPath);
            if (List().Count(e => e.State == OutboxState.Queued) >= MaxQueued)
            {
                throw new InvalidOperationException(
                    $"There are already {MaxQueued} submissions waiting to be sent. Connect to the KOC network to clear them.");
            }
        }

        await using (var file = File.Create(Path.Combine(FolderPath, $"{Safe(entry.Id)}.csv")))
        {
            await predictions.CopyToAsync(file, ct);
        }

        Save(entry);
        return entry;
    }

    /// <summary>Records a successful send and removes it from the queue.</summary>
    public void Sent(string id, double? serverScore)
    {
        lock (_gate)
        {
            Delete(id);
            _ = serverScore; // the caller notifies; the entry's job is done
        }
    }

    /// <summary>
    /// Records a transient failure. After <see cref="MaxAttempts"/> it stops being retried and starts
    /// being somebody's problem, which is better than a queue that spins forever against a wall.
    /// </summary>
    public QueuedSubmission? Failed(string id, string error, DateTime? attemptedUtc = null)
    {
        lock (_gate)
        {
            if (Read(PathFor(id)) is not { } entry)
            {
                return null;
            }

            var attempts = entry.Attempts + 1;
            var updated = entry with
            {
                Attempts = attempts,
                LastAttemptUtc = attemptedUtc ?? DateTime.UtcNow,
                LastError = error,
                State = attempts >= MaxAttempts ? OutboxState.NeedsAttention : OutboxState.Queued,
            };

            Save(updated);
            return updated;
        }
    }

    /// <summary>
    /// Records a refusal — the competition closed, or it is no longer visible.
    /// <para>
    /// The entry stays on disk with the reason. The person did the work; telling them it is gone and
    /// why is the least they are owed, and the predictions are still theirs to use elsewhere.
    /// </para>
    /// </summary>
    public QueuedSubmission? Rejected(string id, string reason)
    {
        lock (_gate)
        {
            if (Read(PathFor(id)) is not { } entry)
            {
                return null;
            }

            var updated = entry with { State = OutboxState.Rejected, LastError = reason };
            Save(updated);
            return updated;
        }
    }

    /// <summary>Forgets an entry entirely — only ever at the user's request.</summary>
    public bool Discard(string id)
    {
        lock (_gate)
        {
            return Delete(id);
        }
    }

    /// <summary>
    /// How long to wait before trying again — doubling, and capped.
    /// <para>
    /// Capped because the network coming back is the event that matters, and a schedule that has grown
    /// to hours would sit idle through it.
    /// </para>
    /// </summary>
    public static TimeSpan BackoffFor(int attempts) =>
        TimeSpan.FromSeconds(Math.Min(300, 15 * Math.Pow(2, Math.Max(0, attempts - 1))));

    private string PathFor(string id) => Path.Combine(FolderPath, $"{Safe(id)}.json");

    private static string Safe(string id) =>
        new([.. id.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_')]);

    private void Save(QueuedSubmission entry)
    {
        Directory.CreateDirectory(FolderPath);
        File.WriteAllText(PathFor(entry.Id), JsonSerializer.Serialize(entry, Json));
    }

    private bool Delete(string id)
    {
        var json = PathFor(id);
        var csv = Path.Combine(FolderPath, $"{Safe(id)}.csv");
        var existed = File.Exists(json);

        try { File.Delete(json); } catch (IOException) { /* best effort */ }
        try { File.Delete(csv); } catch (IOException) { /* best effort */ }

        return existed;
    }

    private static QueuedSubmission? Read(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<QueuedSubmission>(File.ReadAllText(path), Json);
        }
        catch (Exception)
        {
            // One unreadable entry must not hide the rest of somebody's queued work.
            return null;
        }
    }
}
