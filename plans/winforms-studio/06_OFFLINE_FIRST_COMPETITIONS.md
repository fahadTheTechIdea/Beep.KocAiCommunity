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

## The blocker was closed first

This document said not to ship before the API accepted an idempotency key, and it was still open —
nothing in `CompetitionEndpoints` or `CompetitionService` had ever seen one. So that was built before
any of the desktop work, as a platform change:

- `Submission.IdempotencyKey`, unique per (competition, submitter) and **filtered to skip nulls** — the
  ordinary online path sends no key, and a unique index that counted nulls would allow exactly one
  keyless submission per person per competition, ever. The filter's quoting differs between SQLite and
  SQL Server, so `KocDbContext` sets it where it knows which provider is in play. Migrations on both.
- Both submit endpoints read the conventional `Idempotency-Key` header.
- The key is checked **before the quota**, and again **inside the serializable transaction** — the
  upfront check races with a concurrent replay, which is exactly what a client retrying a request it
  never saw the answer to produces.

Six integration tests cover it, including four concurrent replays of one key producing one submission.

## What implementation changed

**`SyncService` takes a two-method interface, not `IKocApiClient`.** The plan's file table had it
depending on the client. That interface is the whole platform surface, and a sync loop that takes all of
it cannot be faked in a test — which in practice means the sync loop does not get tested, and the
research this phase is built on is emphatic that a sync layer tested only online is not tested.
`ISubmissionSender` is *is it reachable* and *send this*, and the convergence property is testable
because of it.

**Quota exhaustion is deliberately not a refusal.** The conflict table lists it as "keep queued, retry
after the quota window", and the code has to actively exclude it from the refusal check — the window
reopens tomorrow, and rejecting would throw away work the server would have taken.

## Acceptance criteria

- [x] Browsing offline shows the last cached list with its age
- [x] The age is always visible when serving cache — never presented as live
- [x] Submitting offline queues and says so, rather than failing
- [x] Reconnecting drains the queue without user action
- [x] A replayed submission is not counted twice — the API gap is closed, with tests
- [x] A submission to a since-concluded competition is rejected with the reason, and the file is kept
- [x] Quota exhaustion retries later rather than rejecting
- [x] The connection indicator reflects reality when checked — **but nothing polls it yet**, see below
- [ ] **Local and server scores are not yet shown together.** The queued entry carries `LocalScore` and
      the sync records the server's, but the "they should differ, and that gap is what teaches
      overfitting" moment is not built. It deserves designing rather than bolting on
- [x] Nothing is ever silently discarded

## Not built: the background poller

The design calls for a hosted service polling `/meta` every ~20 seconds so the queue drains on its own.
What exists is `SyncService.IsReachableAsync` and `DrainAsync`, wired to a **Try now** button on the
outbox page and to the layout's indicator. The loop that calls them unattended is not there.

Deliberate: a `BlazorWebView` desktop has no host builder running background services the way the API
does, so this needs a timer owned by the WinForms shell with its own lifetime and shutdown handling —
and a timer that fires into a disposed scope is the kind of bug that shows up as a mystery crash on
somebody else's machine. The pieces it needs are all here and tested; wiring it is a small, careful
job that should be done with a running window to watch.

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
