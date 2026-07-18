# Phase 04 — API Contracts and Real-Time Events

**Status:** 🟡 PLANNING
**Dependencies:** Phase 02, Phase 03
**Goal:** Build the canonical API surface and the SignalR/Outbox real-time delivery pipeline.

## 1. Goal and dependencies

- Minimal API endpoints under `/api/v1`
- Problem Details, OpenAPI, pagination, filtering, sorting, ETags, idempotency keys, validation
- SignalR hubs for run progress, leaderboard, discussions, project collaboration
- Transactional outbox for guaranteed at-least-once event delivery
- Rate limits and upload-specific limits

## 2. Existing reference behavior

- Beep.ApiServer uses Minimal API + JWT + API key (`Beep.ApiServer/Beep.ApiServer/Program.cs:29-67`).
- Beep.AI.Server uses REST blueprints + SocketIO events (`Beep.AI.Server/Beep.AI.Server/app/__init__.py:505-541`).

## 3. Architecture decisions

| Decision | Choice | Rationale |
|---|---|---|
| API style | Minimal API | Less ceremony than MVC controllers |
| Versioning | `/api/v1` | Standard |
| Documentation | OpenAPI 3.1, Swagger UI in Development | Standard |
| Errors | RFC 9457 Problem Details | Standard |
| Pagination | `?page=&pageSize=` with `Link` headers | Simple |
| ETags | `If-Match`/`ETag` on mutable resources | Concurrency |
| Idempotency | `Idempotency-Key` header for POSTs | Safe retries |
| Rate limits | AspNetCore RateLimiting middleware | Standard |
| Real-time | SignalR via transactional outbox | No dual-write inconsistency |
| Outbox table | `OutboxMessage(Id, Type, Payload, CreatedUtc, ProcessedUtc, RetryCount)` | Reliable delivery |
| Dispatcher | Hosted service in API that reads unprocessed messages and publishes to SignalR | Decoupled from Worker |

## 4. Project-by-project deliverables

### 4.1 Application/Abstractions

- `IApiVersioning`
- `IPagedResult<T>`, `IETaggable`
- `IOutboxWriter` (used by Application services to enqueue events)
- `IDomainEvent` marker interface

### 4.2 Application/RealTime

- `IRunProgressHubClient`, `ILeaderboardHubClient`, `IDiscussionHubClient`, `IProjectHubClient`
- Domain event types: `RunProgressEvent`, `RunCompletedEvent`, `LeaderboardUpdatedEvent`, `DiscussionMessagePostedEvent`, `ProjectMemberAddedEvent`

### 4.3 Infrastructure/Outbox

- `OutboxMessage` entity
- `OutboxWriter` service
- `OutboxDispatcher` hosted service (in API project)
- `OutboxMessageRelay` that publishes to SignalR group based on event type

### 4.4 API

- `Program.cs` registers Minimal API endpoints, OpenAPI, Problem Details, rate limits, SignalR hubs
- `Endpoints/` folder organizes endpoints by domain
- `Filters/` folder holds validation, idempotency, and authorization filters

## 5. Entities and migrations

- `OutboxMessage(Id, Type, PayloadJson, CreatedUtc, ProcessedUtc, RetryCount, LastError)`
- Index on `(ProcessedUtc, CreatedUtc)` for dispatcher query

## 6. API contracts

Documented in `references/API_ROUTE_CATALOG.md`. Initial routes:

```http
GET    /api/v1/me
GET    /api/v1/users/search?q=
GET    /api/v1/discussions?projectId=&page=
POST   /api/v1/discussions
POST   /api/v1/discussions/{id}/replies
POST   /api/v1/datasets
GET    /api/v1/datasets/{id}
POST   /api/v1/datasets/{id}/versions
POST   /api/v1/projects
GET    /api/v1/projects/{id}
POST   /api/v1/projects/{id}/members
POST   /api/v1/workflows
GET    /api/v1/workflows/{id}/versions
POST   /api/v1/workflows/{id}/versions
POST   /api/v1/workflows/{id}/run
GET    /api/v1/runs/{id}
GET    /api/v1/experiments
POST   /api/v1/experiments
GET    /api/v1/models
POST   /api/v1/models/{id}/promote
POST   /api/v1/competitions/{id}/submissions
GET    /api/v1/competitions/{id}/leaderboard
GET    /health
```

## 7. MudBlazor pages and components

This stage does not add pages, but ensures all future pages can call the API via typed clients.

## 8. Security and authorization

- All endpoints require a valid bearer token (Employee minimum)
- Authorization enforced via `[Authorize(Policy = ...)]` filters
- Idempotency-Key requires Employee; reuse the key on retry returns the same response

## 9. Tests

- Unit: validation, idempotency, ETag, pagination, Problem Details
- Integration: end-to-end happy path for each domain endpoint with stub token handler
- Integration: outbox dispatcher delivers events to SignalR clients in correct order
- Integration: rate limiter blocks when exceeded
- Contract: OpenAPI schema matches response payloads

## 10. Verification commands

```bash
dotnet run --project src/Beep.KocAiCommunity.Api
```

Browse `https://localhost:5002/swagger` to confirm OpenAPI is generated. Use a sample token to call `/api/v1/me`.

## 11. Acceptance gate

- OpenAPI schema is generated and accurate
- Pagination, filtering, sorting, ETags work
- Idempotency keys are honored
- Rate limits engage
- Outbox events are persisted and dispatched
- SignalR clients receive events in order
- Problem Details returned for all error paths
- Tests pass

## 12. Risks and deferred work

- Outbox ordering must be guaranteed by ID; document the rule
- SignalR group management can leak; plan group lifecycle per resource
- Idempotency key store must be persisted in the database, not memory, for cross-instance correctness
