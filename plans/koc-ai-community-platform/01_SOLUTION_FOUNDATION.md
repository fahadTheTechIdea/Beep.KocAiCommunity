# Phase 01 — Solution Foundation

**Status:** 🟡 PLANNING
**Dependencies:** Phase 00
**Goal:** Stand up the solution, projects, build/test/lint tooling, Aspire orchestration, and dependency policy.

## 1. Goal and dependencies

Establish the buildable foundation that every later stage assumes:

- 16 projects wired into one solution
- Central package management (no version drift)
- Aspire AppHost that runs Web, API, Worker, and SQL Server (local container)
- Service defaults with health checks, OpenTelemetry, resilience
- Test projects with skeletons that pass on an empty solution
- Architecture tests that lock the dependency direction

## 2. Existing reference behavior

- BeepWeb uses `Beep.Blazor.Server.App` (host) + `Beep.Razor.Components` (RCL) + designer RCLs (BeepWeb/AGENTS.md:6-17).
- Beep.StreamingEvents.Web.Web uses Aspire service defaults (Beep.StreamingEvents/Beep.StreamingEvents.Web/Beep.StreamingEvents.Web.Web/Program.cs:31).
- Beep.OilandGas.Web uses MudBlazor 9.5 + OIDC + scoped page/domain services.

## 3. Architecture decisions

| Decision | Choice | Rationale |
|---|---|---|
| Solution file | `Beep.KocAiCommunity.sln` (slnx) | New format, fewer merge conflicts |
| SDK | `Microsoft.NET.Sdk` for libs; `Microsoft.NET.Sdk.Web` for Web; `Microsoft.NET.Sdk.Worker` for Worker | Standard SDKs |
| Target framework | `net10.0` everywhere | LTS |
| Nullable | enable | Per BeepWeb |
| ImplicitUsings | enable | Per BeepWeb |
| Warnings-as-errors | enable in non-test projects | Strict quality bar |
| Central package management | `Directory.Packages.props` | Single source of truth |
| Directory props | `Directory.Build.props` | Shared analyzer settings |
| global.json | pin to .NET 10 SDK | Reproducibility |
| EditorConfig | yes, checked in | Style consistency |

## 4. Project-by-project deliverables

### 4.1 AppHost project `src/Beep.KocAiCommunity.AppHost/`

- `Microsoft.NET.Sdk` SDK (AppHost)
- References ServiceDefaults, Web, API, Worker
- `AppHost.cs` orchestrates Web, API, Worker, plus SQL Server container
- `WithHttpHealthCheck("/health")` on each
- `WithReference` for API and Worker
- `WaitFor` to enforce startup order

### 4.2 Service defaults project `src/Beep.KocAiCommunity.ServiceDefaults/`

- Standard Aspire extensions
- Health checks: `self`, `db`, `blob`
- OpenTelemetry: ASP.NET Core, HttpClient, EF Core, MudBlazor
- Resilience: standard timeouts, retries, circuit breakers (configurable)
- Service discovery via Aspire

### 4.3 Web project `src/Beep.KocAiCommunity.Web/`

- `Microsoft.NET.Sdk.Web`
- Interactive Server render mode
- MudBlazor providers and theme
- Typed API clients (no direct HTTP calls to API)
- References: `Ui.Shared`, `Ui.Community`, `Ui.Studio`, `Ui.Admin`, `Application` (DTOs only)
- Auth: Microsoft.Identity.Web OIDC

### 4.4 API project `src/Beep.KocAiCommunity.Api/`

- `Microsoft.NET.Sdk.Web`
- Minimal API endpoints under `/api/v1` and `/admin/api/v1`
- Bearer JWT validation via Microsoft.Identity.Web
- OpenAPI generation (Swagger UI in development only)
- References: `Application`, `Infrastructure`, `Domain`, `Contracts`, `Connectors.Abstractions`

### 4.5 Worker project `src/Beep.KocAiCommunity.Worker/`

- `Microsoft.NET.Sdk.Worker`
- Background hosted services:
  - `RunExecutionService` (workflow runs)
  - `OutboxDispatcher` (SignalR delivery)
  - `ExperimentSinkService` (outbound run-events)
  - `HealthMonitorService` (admin dashboard)
- References: `Application`, `Infrastructure`, `Domain`, `ML`, `Workflow`

### 4.6 Contracts project `src/Beep.KocAiCommunity.Contracts/`

- DTOs shared across Web, API, Worker
- No references to EF Core or ML.NET

### 4.7 Domain project `src/Beep.KocAiCommunity.Domain/`

- Pure entity types and domain enums
- No EF Core attributes
- No references to Infrastructure or ML.NET

### 4.8 Application project `src/Beep.KocAiCommunity.Application/`

- Service interfaces (`IProjectService`, `IExperimentService`, etc.)
- Typed settings contracts
- DTO ↔ entity mapping
- References: Domain only

### 4.9 Infrastructure project `src/Beep.KocAiCommunity.Infrastructure/`

- `DbContext`, EF configurations, migrations
- `IArtifactStore` implementations
- Email sender, file upload helpers
- References: Application, Domain
- Two migration assemblies: `Infrastructure.SqliteMigrations` and `Infrastructure.SqlServerMigrations`

### 4.10 ML project `src/Beep.KocAiCommunity.ML/`

- `IMlRuntime`, `IMlTaskHandler`, `INodeExecutor` implementations
- O&G node catalog
- References: Application, Domain, Workflow
- MLContext lifetime: scoped

### 4.11 Workflow project `src/Beep.KocAiCommunity.Workflow/`

- `WorkflowDefinition` JSON contract
- Compiler (cycle detection, topological sort, type compatibility)
- Compiler output: typed execution plan
- References: Application, Domain

### 4.12 UI shared project `src/Beep.KocAiCommunity.Ui.Shared/`

- MudBlazor providers and base components
- Theme tokens
- Layout primitives
- References: MudBlazor only

### 4.13 Community UI project `src/Beep.KocAiCommunity.Ui.Community/`

- MudBlazor pages for collaboration surface
- References: Ui.Shared, Application (DTOs only)

### 4.14 Studio UI project `src/Beep.KocAiCommunity.Ui.Studio/`

- MudBlazor pages for ML surface
- References: Ui.Shared, Application (DTOs only)

### 4.15 Admin UI project `src/Beep.KocAiCommunity.Ui.Admin/`

- MudBlazor admin pages
- References: Ui.Shared, Application (DTOs only)

### 4.16 Test projects

```
tests/
├── Beep.KocAiCommunity.UnitTests/
├── Beep.KocAiCommunity.IntegrationTests/
├── Beep.KocAiCommunity.ComponentTests/
├── Beep.KocAiCommunity.ArchitectureTests/
└── Beep.KocAiCommunity.EndToEndTests/
```

Each test project targets `net10.0` and uses xUnit 2.9.3, FluentAssertions, and (for component) bUnit.

## 5. Entities and migrations

No entities in this stage. The skeleton `DbContext` is empty.

## 6. API contracts

No API contracts in this stage. `Program.cs` exposes a placeholder `/health` endpoint.

## 7. MudBlazor pages and components

- `App.razor` with `<Router>`, `<HeadOutlet>`
- `MainLayout.razor` with providers stub
- `NavMenu.razor` stub

## 8. Security and authorization

- Anonymous access only during this stage
- No Entra integration yet
- HTTPS redirection enabled in Development
- Default security headers middleware stub

## 9. Tests

- UnitTests: empty class
- IntegrationTests: empty class
- ComponentTests: empty class
- ArchitectureTests: enforces no project references to forbidden directions (Web → Application OK, Web → Infrastructure NOT OK)
- EndToEndTests: empty class

## 10. Verification commands

```bash
dotnet restore
dotnet format --verify-no-changes
dotnet build --no-restore -warnaserror
dotnet test --no-build
```

Aspire launch:

```bash
dotnet run --project src/Beep.KocAiCommunity.AppHost
```

## 11. Acceptance gate

- All projects compile
- All test projects run and pass with zero tests
- Architecture tests pass (dependency direction enforced)
- Aspire AppHost launches all services
- `/health` returns 200 in each service
- Format verification passes

## 12. Risks and deferred work

- Aspire workload may not be installed locally; verify with `dotnet workload list`
- CI pipeline not configured until a later stage
- Performance baseline not yet captured
