# 06 — Offline-first competitions

> **Depends on:** 01 (logging). **Independent of** 03–05.

## Context

The desktop's offline guarantee has one hole, and it is the feature the platform is named for. Today:

- Browsing competitions offline shows *"Connect to the KOC network to compete"* — honest, but the list
  is unavailable even though it was on screen ten minutes ago.
- **Submitting offline just fails.** The work is done, the pipeline ran, the score is known — and the
  submission is lost because the network was not there at that moment.

An engineer at a rig site who builds a good pipeline on Tuesday should not have to rebuild it on
Thursday to submit it.

## Scope

**In**

- Cached competition list and detail for offline browsing, clearly marked as cached
- Durable outbox for submissions made offline
- Background sync when the network returns, with idempotent replay
- Conflict handling for a competition that has since closed or changed
- Honest connection state throughout

**Out**

- Offline leaderboards beyond the last cached snapshot. A leaderboard's value is being live; a
  three-day-old one shown as current would mislead.
- Offline discussions or community. Per Phase 00, out of scope for the desktop.
- Peer-to-peer sync between desktops.

## Design

The shape from Phase 00 §4: **local store · durable outbox · sync worker · idempotent API · conflict
strategy.** All five are needed; skipping the last is how sync layers corrupt things quietly.

### Cache

```
workspace/
  cache/
    competitions.json      ← list, with fetchedUtc
    competition-{id}.json  ← detail + leaderboard snapshot, with fetchedUtc
```

Written on every successful online fetch. Read when a fetch fails. The UI must **always** show the age
when serving cache — *"Showing data from 2 hours ago — not connected"* — because a stale leaderboard
presented as live is worse than no leaderboard.

Cache is disposable: deleting it costs a refresh, nothing more. It never holds anything the platform
would not serve to this user anyway.

### Outbox

```
workspace/
  outbox/
    {timestamp}-{id}.json   ← the queued submission
    {timestamp}-{id}.csv    ← the predictions
```

Each entry records, per **D8**:

```jsonc
{
  "id": "…",                       // idempotency key — survives retries
  "competitionId": "…",
  "competitionTitle": "…",         // for the UI without a network call
  "queuedUtc": "…",
  "workflowId": "…",               // lineage
  "localScore": 0.87,              // what we measured locally
  "competitionStatusWhenQueued": "active",
  "revealUtcWhenQueued": "…",      // enough to detect that it has since closed
  "attempts": 0,
  "lastError": null
}
```

The snapshot fields are the point. On replay, we can tell whether the competition is still the thing it
was — without version metadata, conflict detection is unreliable.

### Sync

A hosted service polling connectivity — a cheap `GET /api/v1/meta` rather than a ping, because what
matters is whether *our API* is reachable, not whether the internet is.

On reachable:

1. Take the oldest entry.
2. Replay it with `id` as the idempotency key.
3. On success — record the server's score, remove from outbox, notify.
4. On a **conflict** (409/400 because the competition closed) — move to `outbox/rejected/` with the
   reason and notify. Never silently discard: the user did the work.
5. On a **transient** failure — exponential backoff, capped, `attempts++`. After 5, park it as needs
   attention rather than retrying forever.

> **The API needs idempotency to make this safe.** The platform tracker records idempotency keys as an
> open gap in Phase 04. Until they exist, a retried submission may be counted twice — which matters
> because submissions are quota-limited. **This phase should not ship before that gap is closed**, and
> that is a platform change, not a desktop one.

### Connection state

One shared indicator, three states — Online, Offline, Syncing (n queued) — visible in the app bar rather
than discovered per page. Clicking it shows the outbox: what is queued, what failed, what was rejected
and why.

### Conflicts

| Situation on replay | Behaviour |
|---|---|
| Competition still active | Submit; record the server score |
| Concluded since queueing | Reject with *"this competition concluded on {date}"*; keep the file |
| Daily quota now exhausted | Keep queued, retry after the quota window |
| Competition deleted or now invisible | Reject with the reason; keep the file |
| Server score differs from local | Show both. They *should* differ — local is on training data, the server scores the hidden set. This is a teaching moment, not an error |

That last row is worth building deliberately. The gap between a local score and a leaderboard score is
the thing that teaches overfitting, and presenting it as a discrepancy would waste it.

## Files

| File | Change |
|---|---|
| `Desktop.Local/LocalCompetitionCache.cs` | New — read/write with age |
| `Desktop.Local/SubmissionOutbox.cs` | New — enqueue, list, dequeue, reject |
| `Desktop.Local/SyncService.cs` | New — hosted service, connectivity + replay |
| `Desktop.Local/ConnectionState.cs` | New — observable state for the UI |
| `Desktop.Local/LocalKocApiClient.cs` | Serve from cache on failure; enqueue on submit-while-offline |
| `WinForms/Components/Competitions.razor` | Cache-age banner; queued state |
| `WinForms/Components/DesktopLayout.razor` | Connection indicator |
| `WinForms/Components/Outbox.razor` | New — queued, failed, rejected |
| `Api/Endpoints/CompetitionEndpoints.cs` | Accept and honour an idempotency key *(platform change)* |

## Acceptance criteria

- [ ] Browsing offline shows the last cached list with its age
- [ ] The age is always visible when serving cache — never presented as live
- [ ] Submitting offline queues and says so, rather than failing
- [ ] Reconnecting drains the queue without user action
- [ ] A replayed submission is not counted twice *(requires the API idempotency gap closed)*
- [ ] A submission to a since-concluded competition is rejected with the date, and the file is kept
- [ ] Quota exhaustion retries later rather than rejecting
- [ ] The connection indicator reflects reality within ~30 s of a change
- [ ] Local and server scores are shown together, with the difference explained
- [ ] Nothing is ever silently discarded

## Tests

| Test | Level |
|---|---|
| Cache round-trips and reports its age | Unit |
| Offline read falls back to cache; online read refreshes it | Unit |
| Outbox survives a restart | Unit |
| Replay uses the same idempotency key across retries | Unit |
| A concluded competition rejects, and the file is retained | Unit |
| Backoff grows and caps; parks after 5 attempts | Unit |
| Simulated offline → queue → online → drain, end to end | Integration |
| Convergence: any interleaving of queue and reconnect ends with each submission sent exactly once or explicitly rejected | Property-based |

That last one follows the research directly: a sync layer tested only online is not tested.

## Risks

| Risk | Mitigation |
|---|---|
| **Double submission against a quota** | Blocked on API idempotency. Do not ship this phase before it |
| Stale cache mistaken for live | Age shown always; never silent |
| Outbox grows unbounded when the network is gone for weeks | Cap at 50 queued; refuse politely beyond, saying why |
| A user believes a queued submission has scored | Queued entries show no score and are labelled **Queued** |
| Connectivity check hits a captive portal and reports online | Check our API's `/meta` and require a valid response body, not a 200 |
