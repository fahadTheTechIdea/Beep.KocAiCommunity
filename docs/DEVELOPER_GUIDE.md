# Developer Guide — Beep.KocAiCommunity

A practical manual for engineers working on the KOC Training & Career Development platform: how the
solution is put together, how to run it, and how to extend it safely.

> New here? Read the root [`README.md`](../README.md) first for the product overview, then this guide.
> Deploying? See [`docs/DEPLOYMENT.md`](DEPLOYMENT.md). Administering a running instance? See
> [`docs/ADMIN_GUIDE.md`](ADMIN_GUIDE.md).

---

## 1. What it is

An internal, Kaggle-style AI/ML learning + competition platform for KOC: guided learning tracks,
competitions with live/final leaderboards, a visual ML **Studio** (node-based pipeline designer over
ML.NET + DuckDB), a model registry, community discussions, gamification, and a platform-admin console.
There are two front-ends over one API: the **Blazor Web app** and a **WinForms desktop app** that
reuses the same designer and runs offline.

## 2. Prerequisites

- **.NET SDK 10** (pinned in `global.json`).
- Windows is required only for the **desktop app** (`net10.0-windows`) and SQL Server LocalDB; the rest
  is cross-platform.
- No database to install for dev — the API auto-creates a local **SQLite** file.

## 3. Solution layout

```
src/
  Domain            Entity types + enums. No EF, no framework.
  Contracts         DTOs shared across every host and both front-ends.
  Application       Service interfaces, role/policy constants, domain events, ML abstractions.
  Workflow          Workflow compiler (validate + topological order).
  ML                ML.NET + DuckDB runtime: node handlers, PluginNodeExecutor, AutoML trainer.
  Infrastructure    EF Core (DbContext, configs, migrations), service impls, storage, seeders.
  Infrastructure.SqlServerMigrations   Provider-specific SQL Server migrations.
  Client            Framework-agnostic HTTP API client (IKocApiClient) + dev identity. Web + desktop.
  Ui.Shared         MudBlazor theme + shared components (the "KOC blueprint" look).
  Ui.Studio         The workflow designer RCL (shared by Web and desktop).
  Ui.Community/Admin Feature RCLs.
  ServiceDefaults   Aspire defaults + shared security wiring (auth, policies, current user).
  Web               Blazor Web App (Interactive Server). Calls the API; never the DB directly.
  Api               Minimal API (/api/v1) + SignalR hub + transactional-outbox dispatcher.
  Worker            Background worker (durable jobs: training runs, etc.).
  Desktop.Local     Offline in-process Studio engine for the desktop (LocalKocApiClient).
  WinForms          Desktop app hosting the Studio designer via BlazorWebView.
  AppHost           .NET Aspire orchestration.
tests/              UnitTests, IntegrationTests, ComponentTests (bUnit), ArchitectureTests, EndToEndTests.
```

**Dependency direction is enforced** by `ArchitectureTests`: Domain/Application stay free of EF Core,
ASP.NET, MudBlazor, and ML.NET; the Web talks to the API, never the database.

## 4. Run it locally

Two terminals (the API migrates + seeds a local SQLite DB and enables dev auth):

```bash
# API on :5250 (migrate + seed + dev auth)
ASPNETCORE_ENVIRONMENT=Development Seed__Enabled=true ASPNETCORE_URLS=http://localhost:5250 \
  dotnet run --project src/Beep.KocAiCommunity.Api

# Web on :5150 (calls the API)
ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://localhost:5150 \
  dotnet run --project src/Beep.KocAiCommunity.Web
```

Open <http://localhost:5150>. Aspire alternative: `dotnet run --project src/Beep.KocAiCommunity.AppHost`.

**Desktop app** (offline; no API needed for the designer + local runs):

```bash
dotnet run --project src/Beep.KocAiCommunity.WinForms
```

## 5. Identity & auth in dev

- **Dev personas.** With no Entra tenant, the Web forwards a selected persona to the API via
  `X-Dev-User`/`X-Dev-Roles` headers (`DevIdentity` + `DevIdentityHandler` in `Client`). The app-bar
  **"view as"** switcher changes persona live. Personas: `guest, employee, teamleader, manager, dceo,
  ceo, compadmin, platformadmin` (default **platformadmin**, so every area is visible out of the box).
- **The API** resolves the current user from claims (`IKocCurrentUser` → `ClaimsKocCurrentUser`). In dev
  those claims come from the forwarded headers (`DevAutoAuthHandler`); in prod from Entra or Windows auth.
- **Roles**: positions (`Employee → TeamLeader → Manager → DCEO → CEO`) + function roles
  (`PlatformAdmin, CompetitionAdmin, LearningAdmin, Auditor`). Policies live in `KocPolicies` /
  `SecurityExtensions` (`RequireEmployee`, `RequirePlatformAdmin`, …).
- **Production**: set `AzureAd` config for Entra, or `WindowsAuth:Enabled=true` for intranet Windows SSO
  (IIS Windows Authentication). See the README "Production auth" section.

## 6. Data & migrations

Dual-provider EF Core: **SQLite** (dev/test) and **SQL Server** (prod), chosen by `Database:Provider`.
Migrations are **generated for both providers**:

```bash
# SQLite (default provider project)
dotnet ef migrations add <Name> \
  --project src/Beep.KocAiCommunity.Infrastructure \
  --startup-project src/Beep.KocAiCommunity.Api

# SQL Server (its own migrations assembly)
dotnet ef migrations add <Name> \
  --project src/Beep.KocAiCommunity.Infrastructure.SqlServerMigrations \
  --startup-project src/Beep.KocAiCommunity.Infrastructure.SqlServerMigrations
```

Entity configs live under `Infrastructure/Persistence/Configurations`. Seeders (dev org tree, learning
tracks, demo data) live under `Infrastructure/Organization` and `Infrastructure/Admin`.

## 7. The Studio pipeline engine (nodes)

The designer is a graph of typed **nodes** executed node-by-node. The engine is **plug-and-play**:

- Every node kind is one **`IPipelineNodeHandler`** in `src/Beep.KocAiCommunity.ML/Nodes/`. It exposes a
  `NodeDescriptor` (kind, category, ports, typed parameters) and an `Execute` method.
- **`PluginNodeRegistry`** derives the whole catalog from the registered handlers; **`PluginNodeExecutor`**
  dispatches. DI auto-registers every handler in the ML assembly — **adding a node kind = adding one
  handler class**; the catalog (`GET /api/v1/ml/nodes`), the compiler's known kinds, and execution all
  pick it up automatically.
- Data flows on a uniform **`PipelineTable`** (CSV-file-backed) contract, so **ML.NET** nodes and
  **DuckDB** (SQL/ETL) nodes interoperate in either order. DuckDB nodes: `sql`, `sql-filter`, `group-by`,
  `sort`, `distinct`, `join-dataset`, `union-dataset` (the last two pull in a *second* dataset).
- ~37 node kinds total across categories: Source, Data (DuckDB), Prepare, Shape, Transform, Split, Model,
  Evaluate.

**To add a node:** create a handler class in `ML/Nodes/` implementing `IPipelineNodeHandler`; that's it.
Verify with `NodeCatalogTests` (registry ↔ compiler known-kinds parity) and the designer palette (it is
API-driven — `Ui.Studio` renders from `GetMlNodesAsync()`; only the icon/colour live client-side in
`NodeVisuals`).

## 8. Workflows, competitions, models

- **Workflows registry** (`/workflows`): versioned pipelines — save draft → publish an immutable snapshot
  → export/import → run. A workflow can optionally target a **competition** (`Workflow.CompetitionId`).
- **Competitions** (`/compete`): creation is **grant-gated** — a user needs an active
  `CompetitionCreatorGrant` (or `PlatformAdmin`) and may only target audiences up to their granted
  `MaxScope` (Team ≤ Group ≤ Directorate ≤ Company). Enforced in `CompetitionService.CreateAsync`.
  Submissions turn a Studio pipeline into a scored leaderboard entry.
- **Model registry** (`/models`): register → two-approval promote → deploy/retire; inference serving.

## 9. The WinForms desktop app

- **BlazorWebView hybrid**: hosts the exact `Ui.Studio` Blazor components in a WinForms window — the same
  designer, on the desktop.
- **Offline-first**: `Desktop.Local`'s `LocalKocApiClient` runs the Studio surface **in-process** — node
  catalog from `PluginNodeRegistry`, datasets from local CSVs, **pipeline runs** via `PluginNodeExecutor`,
  workflows as local JSON. Only competitions call the API (`RemoteFallbackKocApiClient` delegates the
  rest). Workspace: `%LOCALAPPDATA%/KocStudio/{datasets,workflows,temp}`.
- **Identity**: the real signed-in Windows/Entra user by default (`IEnvironmentUserProvider` →
  `WindowsEnvironmentUserProvider`); dev personas remain as a Settings override. Dept/profile enrich via
  the same seam once the KOC directory API is wired.
- Publish: `dotnet publish src/Beep.KocAiCommunity.WinForms -c Release -r win-x64` (bundles ML.NET/DuckDB
  `win-x64` natives). Requires the WebView2 runtime (standard on Win10/11).

## 10. Testing & the definition of done

Every change must pass the **DoD gate** before commit:

```bash
dotnet build  Beep.KocAiCommunity.slnx -warnaserror
dotnet format Beep.KocAiCommunity.slnx --verify-no-changes
dotnet test   Beep.KocAiCommunity.slnx
```

Test suites: **UnitTests** (engine, services, local desktop engine), **IntegrationTests** (API over an
in-memory SQLite DB + a test auth scheme), **ComponentTests** (bUnit), **ArchitectureTests** (dependency
rules), **EndToEndTests**. Warnings are errors for non-test projects. Commit each logical change on
`master` with a descriptive message; update the plan docs + `plans/koc-ai-community-platform/MASTER_TODO_TRACKER.md`.

Note: `MlDeterminismTests` (AutoML) can flake under full-suite parallel load — re-run to confirm.

## 11. Common extension recipes

| Task | Where |
|---|---|
| Add a pipeline node | one `IPipelineNodeHandler` in `ML/Nodes/` |
| Add an API endpoint | a `Map…Endpoints` group in `Api/Endpoints`, mapped in `Api/Program.cs` |
| Add a DTO | `Contracts/<area>` (shared by all hosts) |
| Add a Web page | a `.razor` under `Web/Components/Pages` (or a feature RCL) |
| Add a client method | `IKocApiClient` + `KocApiClient` in `Client` |
| Add a service | interface in `Application`, impl in `Infrastructure`, register in `Infrastructure/DependencyInjection.cs` |
| Enrich signed-in user (dept/roles) | implement `IEnvironmentUserProvider` (`Client`) against the KOC directory API |

## 12. Where to read more

- Architecture decisions & phase history: `plans/koc-ai-community-platform/` (numbered docs +
  `MASTER_TODO_TRACKER.md`).
- Node engine: `16_PLUGGABLE_NODE_ENGINE.md`, `17_DUCKDB_ENGINE.md`.
- Desktop app: `19_WINFORMS_DESKTOP_STUDIO.md` (+ 19a–19d).
