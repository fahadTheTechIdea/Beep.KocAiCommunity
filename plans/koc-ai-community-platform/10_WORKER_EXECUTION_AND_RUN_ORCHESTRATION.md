# Phase 10 — Worker Execution and Run Orchestration

**Status:** ✅ DONE (implemented 2026-07-19) — EF-backed durable queue (`EfJobQueue`) with atomic `ExecuteUpdate` claiming (single-owner leases, provider-portable on SQLite + SqlServer), heartbeat lease renewal, exponential backoff retries → dead-letter, cooperative cancellation, and expired-lease crash recovery. `JobProcessor` runs one job (dispatch → heartbeat → complete/fail/cancel); `JobExecutionService` runs N concurrent lease loops with graceful drain (SQLite=1). Run progress streams via the outbox → `LeaderboardHub` `run:{id}` group. `ModelTrainingJobHandler` runs real AutoML training out-of-band. `/api/v1/runs` (create/get/list/cancel/logs/attempts) + typed client + `/runs` monitor page with live logs. Dual-provider `AddJobs` migrations. Builds `-warnaserror` clean; 16 unit + 5 integration tests pass.
**Deferred within phase:** SQL Server multi-worker concurrency is wired (config `Jobs:MaxConcurrency`) but only exercised at 1 in tests; memory-limit hint not enforced (timeout is via lease expiry + cooperative cancel); the Worker reuses the API's outbox dispatcher (shared DB) rather than its own; run-detail component test deferred.

---
_Original plan below._

**Dependencies:** Phase 09
**Goal:** EF-backed durable job queue with leases, retries, cancellation, and real-time progress through SignalR.

## 1. Goal and dependencies

- EF-backed durable job queue
- Leases, heartbeat, retries, cancellation, timeout, crash recovery
- Provider-aware concurrency: SQL Server supports multiple workers; SQLite supports one
- Real-time progress through SignalR via transactional outbox (Phase 04)
- Resource limits and graceful shutdown
- No arbitrary user-uploaded script execution

## 2. Existing reference behavior

- Beep.AI.MLStudio: `app/services/workflow/runner.py:1-1872` (run lifecycle), `scheduler.py:1-188` (cron), `file_watcher.py:1-264` (triggers).
- Beep.AI.Server: `app/services/wizard/wizard_admin_service.py:26` (BackgroundService pattern).
- Microsoft docs: [Background tasks with hosted services in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services?view=aspnetcore-10.0).

## 3. Architecture decisions

| Decision | Choice | Rationale |
|---|---|---|
| Queue storage | EF table | Provider-portable, durable |
| Lease | Heartbeat + lease expiration | Standard pattern |
| Retry policy | Exponential backoff with jitter, max attempts | Standard |
| Cancellation | Cooperative cancellation through token | Standard |
| Concurrency | Configurable per worker; SQLite = 1; SQL Server = N | Provider-aware |
| Real-time | Outbox → SignalR | Per Phase 04 |
| Resource limits | Configurable timeout, max memory hint | Standard |
| Shutdown | Graceful drain of in-flight runs | Standard |

## 4. Project-by-project deliverables

### 4.1 Domain

- `Job`, `JobAttempt`, `JobLog`

### 4.2 Application

- `IJobQueue`, `IJobDispatcher`, `IJobRunner`, `IRunProgressReporter`
- `JobDescriptor` (id, type, payload, priority, retry policy)

### 4.3 Infrastructure

- EF Core configurations for Job and JobAttempt
- `EfJobQueue` implementation with provider-aware row locking (SQL Server `UPDLOCK,ROWLOCK`, SQLite `BEGIN IMMEDIATE`)
- Outbox event publishers

### 4.4 Worker

- `RunExecutionService : BackgroundService` (workflow runs)
- `OutboxDispatcherService : BackgroundService` (consumes outbox, publishes to SignalR groups via API)
- `JobHeartbeatService : BackgroundService` (renews leases)
- `ResourceLimitConfig`

## 5. Entities and migrations

```csharp
public class Job : AuditableEntity
{
    public string Type { get; set; } = default!;
    public string PayloadJson { get; set; } = default!;
    public string Status { get; set; } = "pending"; // pending, leased, running, succeeded, failed, cancelled, deadletter
    public int AttemptCount { get; set; }
    public int MaxAttempts { get; set; } = 5;
    public DateTime? LeaseExpiresUtc { get; set; }
    public string? LeaseOwnerId { get; set; }
    public DateTime? LastHeartbeatUtc { get; set; }
    public DateTime? NextAttemptUtc { get; set; }
    public DateTime? StartedUtc { get; set; }
    public DateTime? CompletedUtc { get; set; }
    public string? LastError { get; set; }
    public int Priority { get; set; }
}

public class JobAttempt : AuditableEntity
{
    public Guid JobId { get; set; }
    public int AttemptNumber { get; set; }
    public DateTime StartedUtc { get; set; }
    public DateTime? CompletedUtc { get; set; }
    public string Status { get; set; } = default!;
    public string? ErrorJson { get; set; }
    public string? WorkerId { get; set; }
}

public class JobLog : AuditableEntity
{
    public Guid JobId { get; set; }
    public DateTime LoggedUtc { get; set; }
    public string Severity { get; set; } = default!;
    public string Message { get; set; } = default!;
    public string? PayloadJson { get; set; }
}
```

## 6. API contracts

```http
POST   /api/v1/runs
GET    /api/v1/runs/{id}
POST   /api/v1/runs/{id}/cancel
GET    /api/v1/runs/{id}/logs
GET    /api/v1/runs/{id}/attempts
```

## 7. MudBlazor pages and components

- `Pages/Studio/Runs/Index.razor` (recent runs)
- `Pages/Studio/Runs/Detail.razor` (live logs via SignalR)
- `Components/Studio/RunProgress.razor`
- `Components/Studio/RunLog.razor`

## 8. Security and authorization

- Project members can view runs for their workflows
- Project owners and PlatformAdmin can cancel runs
- All run events written to the audit envelope

## 9. Tests

- Unit: lease expiration, retry policy, backoff calculation
- Integration: queue persistence across worker restarts, cancellation, duplicate-claim prevention
- Integration: outbox delivers events to SignalR groups in order
- Component: run detail page renders live updates

## 10. Verification commands

```bash
dotnet test tests/Beep.KocAiCommunity.IntegrationTests --filter "FullyQualifiedName~Jobs|FullyQualifiedName~Runs"
```

## 11. Acceptance gate

- Runs survive worker restart
- Cancellation works
- Duplicate claims are prevented
- Progress is delivered via SignalR
- Resource limits enforced
- Tests pass

## 12. Risks and deferred work

- Provider-specific row locking requires careful EF Core query translation
- Outbox ordering must be guaranteed by ID
- Distributed leases across multiple Worker instances need stable worker IDs
