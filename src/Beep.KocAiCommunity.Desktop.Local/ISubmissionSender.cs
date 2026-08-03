using Beep.KocAiCommunity.Client;

namespace Beep.KocAiCommunity.Desktop.Local;

/// <summary>
/// The two things draining a queue needs from the platform: whether it is there, and a way to send.
/// <para>
/// Deliberately narrow. <see cref="IKocApiClient"/> is the whole platform surface, and a sync loop that
/// takes all of it cannot be tested without standing up a fake of the entire API — which usually means
/// the sync loop is not tested, which is the one thing the research on offline-first is emphatic about.
/// </para>
/// </summary>
public interface ISubmissionSender
{
    /// <summary>Whether <em>our</em> API answers — not whether the internet is up.</summary>
    Task<bool> IsReachableAsync(CancellationToken ct = default);

    /// <summary>
    /// Sends a queued submission. <paramref name="idempotencyKey"/> is the entry's own id, constant
    /// across every retry, which is what stops a replay spending a second attempt against the quota.
    /// </summary>
    Task<double?> SendAsync(
        Guid competitionId, Stream predictions, string fileName, string idempotencyKey, CancellationToken ct = default);
}

/// <summary>
/// The real one. Whatever client it is handed is the platform: on a workstation configured with the
/// platform database that is the direct client, which opens the database the website opens; elsewhere
/// it is an HTTP client. Either way the submission is recorded once, against the same service.
/// </summary>
public sealed class ApiSubmissionSender(IKocApiClient api) : ISubmissionSender
{
    public async Task<bool> IsReachableAsync(CancellationToken ct = default)
    {
        try
        {
            // A valid body, not a 200: a captive portal answers 200 to everything, and reporting
            // "online" behind one would drain the queue into a login page.
            return await api.GetPlatformMetaAsync(ct) is not null;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public async Task<double?> SendAsync(
        Guid competitionId, Stream predictions, string fileName, string idempotencyKey, CancellationToken ct = default)
    {
        var result = await api.SubmitAsync(competitionId, predictions, fileName, idempotencyKey, ct);
        return result?.Score;
    }
}

/// <summary>
/// Studio with no platform behind it — no database configured, no website to post to.
/// <para>
/// This exists because the honest answer is "there is nowhere to send this", and the alternative was
/// worse than it looks. Handing the sync loop the local façade instead makes draining pathological:
/// the façade's <c>SubmitAsync</c> is the one that <em>enqueues</em>, and with no platform behind it
/// the send fails, is swallowed, and the entry is written back to the outbox — while returning without
/// throwing, which the sync loop reads as success. The queue would churn every entry forever and
/// report each one as sent.
/// </para>
/// </summary>
public sealed class NoPlatformSubmissionSender : ISubmissionSender
{
    /// <summary>Never reachable: there is no platform configured on this machine to reach.</summary>
    public Task<bool> IsReachableAsync(CancellationToken ct = default) => Task.FromResult(false);

    public Task<double?> SendAsync(
        Guid competitionId, Stream predictions, string fileName, string idempotencyKey, CancellationToken ct = default) =>
        throw new NotSupportedException(
            "This copy of KOC Studio has no platform configured, so there is nowhere to send a "
            + "submission. Set the platform database in settings, or submit from the website.");
}
