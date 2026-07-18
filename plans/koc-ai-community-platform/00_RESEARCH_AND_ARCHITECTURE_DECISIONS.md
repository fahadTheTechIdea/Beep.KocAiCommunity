# Phase 00 — Research and Architecture Decisions

**Status:** 🟡 PLANNING — research complete; decisions recorded
**Goal:** Capture every research finding and architecture decision needed before any code is written.

## 1. Reference applications inspected

| Reference | Stack | Outcome |
|---|---|---|
| `Beep.AI.Community` | Python 3.11 / Flask 3 / SQLAlchemy 2 / SQLite | Behaviour reference for Community surface; **not a code template** |
| `Beep.AI.MLStudio` | Python 3.11 / Flask 3 / SQLAlchemy 2 / SQLite / sklearn / jsPlumb | Behaviour reference for Studio surface; **not a code template** |
| `Beep.AI.Server` | Python 3.11 / Flask 3 | Reference for RBAC, audit envelope, admin decorator |
| `Beep.ML.NET` | .NET 6/7 ML.NET 3.0, WinForms | Reference for ML.NET abstractions and AutoML flow |
| `Beep.AI.Shared` | .NET 6-9, ML.NET contracts | Source of `IMLTrain`, `IMLPredict`, `IMLEval`, `ITrainedModel`, `IMLProcess` |
| `BeepWeb` | .NET 10 / MudBlazor 9.7 / Blazor Server | Structural template — host + RCL + tests |
| `Beep.OilandGas.Web` | .NET 10 / MudBlazor 9.5 / OIDC | Auth + branding + navigation template |
| `Beep.StreamingEvents.Web.Web` | .NET 10 / MudBlazor 9.4 / Fluxor | Page/domain service pairing pattern; Aspire defaults |
| `Beep.ApiServer` | .NET 8 / EF Identity / JWT | Reference API server with OpenAPI dual security |
| `Beep.Foundation.IdentityServer` | .NET 8 / OpenIddict | Auth-only OIDC template (not in this repo's source) |

Detailed file:line references in the exploration reports (cached locally; see `references/TECHNOLOGY_MATRIX.md`).

## 2. Decisions and rationale

### 2.1 Platform

| Decision | Choice | Rationale |
|---|---|---|
| Runtime | .NET 10 LTS (10.0.10) | LTS through November 2028 |
| App model | Blazor Web App with global Interactive Server render mode | Native Blazor, SignalR-backed; matches Beep.OilandGas.Web and Beep.StreamingEvents.Web.Web |
| MudBlazor | 9.7.0 | Latest stable release; already used by BeepWeb |
| UI library | MudBlazor only | Per BeepWeb convention; no Bootstrap HTML, no custom hex |

### 2.2 Solution shape

| Decision | Choice | Rationale |
|---|---|---|
| Deployment shape | Web + API + ML Worker | Isolates long-running ML jobs from interactive request path |
| Orchestration | .NET Aspire AppHost | Built-in health checks, telemetry, service discovery, resilience |
| Project count | 16 projects (5 apps + 5 libs + 5 test projects + 1 AppHost) | Per-stage granular layering |

### 2.3 Authentication and authorization

| Decision | Choice | Rationale |
|---|---|---|
| Authentication | Microsoft Entra ID workforce tenant (KOC only) | Single-tenant KOC deployment; no external users |
| App | Microsoft.Identity.Web 4.13.2 | Microsoft-blessed pattern for Entra in ASP.NET Core |
| App roles | Position levels `Employee`, `TeamLeader`, `Manager`, `DCEO`, `CEO`; platform functions `PlatformAdmin`, `CompetitionAdmin`, `LearningAdmin`, `Auditor` | Position levels mirror the KOC reporting line; function roles are granted on top |
| Org hierarchy | `OrgUnit` tree: Team ⊂ Group ⊂ Directorate ⊂ Company (KOC) | Every person belongs to a Team; supervision and visibility roll up this tree |
| Supervisory scope | Each position level sees a rollup dashboard for the org subtree beneath it (Team Leader→Team, Manager→Group, DCEO→Directorate, CEO→Company) | Managers supervise participation; they do not compete for their reports |
| Resource visibility | `VisibilityScope` (Team/Group/Directorate/Company) + `VisibilityOrgUnitId` on competitions, datasets, projects | Creator chooses "who can see it" at creation; replaces private/project/public-to-KOC |
| Resource permissions | EF-backed permissions per business entity | Defense-in-depth on top of coarse roles and visibility scope |

### 2.4 Persistence

| Decision | Choice | Rationale |
|---|---|---|
| ORM | EF Core only (per user confirmation) | No BeepDM for application data |
| Production provider | SQL Server / Azure SQL | KOC enterprise standard |
| Development provider | SQLite | Cross-platform dev/test; provider-agnostic EF code |
| Migrations | Two migration assemblies | SQL Server-specific and SQLite-specific migrations where DDL diverges |
| Secrets | Data Protection in dev; Key Vault references in production | Standard ASP.NET pattern |
| Artifacts | `IArtifactStore` with local filesystem and Azure Blob providers | Pluggable |

### 2.5 ML.NET

| Decision | Choice | Rationale |
|---|---|---|
| ML.NET version | Microsoft.ML 5.0.0 | Latest stable (Nov 2025); AutoML 0.23.0 matches |
| MLContext lifetime | Singleton per scope | MLContext is heavy and not thread-safe across train/predict |
| Experiment tracking | Native EF tracker using ML.NET `IMonitor` | MLflow has no first-party .NET client |
| Optional sink | `IExperimentSink` with MLflow REST adapter later | Plug-in, not core |
| Heavy ML packages | Deferred to per-project venvs (analog) | Avoid heavy first-startup dependency pull |

### 2.6 Workflow

| Decision | Choice | Rationale |
|---|---|---|
| Editor library | Z.Blazor.Diagrams 3.0.4.1 | Native Blazor, MIT licensed, custom nodes, minimap, virtualization |
| Why not Elsa Studio | Designer only supports Flowchart; UI input validation not implemented; Flowchart-only | Decision made after [Elsa 3 docs review](https://docs.elsaworkflows.io/) |
| Why not jsPlumb | Last release 2021; the existing MLStudio editor is fragile | Verified via [MLStudio workflow builder inspection](references/TECHNOLOGY_MATRIX.md) |
| Workflow JSON | Application-owned versioned `WorkflowDefinition` | Avoid coupling to Z.Blazor.Diagrams internal state |
| Versioning | Immutable versions, drafts, publishing | Proven pattern from MLStudio |

### 2.7 Background execution

| Decision | Choice | Rationale |
|---|---|---|
| Job queue | EF-backed durable queue | Works across all hosting targets, no Redis dependency |
| Concurrency | SQL Server supports multiple workers; SQLite supports one | Provider-aware |
| Real-time | SignalR via transactional outbox | Avoids dual-write inconsistency |

### 2.8 Purpose, tenancy, audience, domain

| Decision | Choice | Rationale |
|---|---|---|
| Primary purpose | Train and familiarize KOC employees with AI/ML | Confirmed by user 2026-07-17 — this is an internal capability-building tool, not production ML ops and not a product |
| Core surfaces | Learning tracks + internal (Kaggle-style) competitions | The way employees learn and practice; ML Studio is the supporting toolkit |
| Primary participants | Employees (learn and compete); management supervises | Employees "have the most fun"; managers see how their people are doing |
| Tenancy | Single KOC tenant | Confirmed by user 2026-07-17 |
| Audience | KOC employees only | Confirmed by user 2026-07-17 |
| Org model | Team ⊂ Group ⊂ Directorate ⊂ Company (KOC); positions Employee→TeamLeader→Manager→DCEO→CEO | Confirmed by user 2026-07-17 |
| Visibility | Creator picks Team/Group/Directorate/Company audience on competitions, datasets, projects | Confirmed by user 2026-07-17 |
| Domain | Oil & gas only | Confirmed by user 2026-07-17 |
| Domain taxonomy | upstream / midstream / downstream / HSE | Standard O&G segmentation |
| Data integrations | PPDM 39, OpenWells, EcoSys, SAP, AVEVA PI, ADLS Gen2 | Confirmed by user 2026-07-17 |

## 3. Reusable patterns from existing Beep apps

| Pattern | Source | Adoption |
|---|---|---|
| Page/Domain service pairing | `Beep.StreamingEvents.Web.Web/Program.cs:69-114` | Adopt |
| Aspire service defaults | `Beep.StreamingEvents.Web.Web/Program.cs:31,154` | Adopt |
| Identity scaffolding EF | `Beep.Razor.Components/Data/ApplicationDbContext.cs:7-9` | Adopt (rename `ApplicationDbContext` to `KocDbContext`) |
| Theme provider | `Beep.Razor.Components/Theme/BeepThemeProvider.cs:22-80` | Adopt (customize for KOC palette) |
| RBAC bridge pattern | `Beep.OilandGas.Web/Program.cs:83-92, 109-331` | Adopt (rebuilt for Entra app roles) |
| Security headers | `Beep.AI.Community/Beep.AI.Community/app/__init__.py:463-484` | Adopt (rewrite for ASP.NET Core middleware) |
| Workflow runtime contracts | `Beep.AI.MLStudio/Beep.AI.MLStudio/app/services/workflow/contracts.py:1-299` | Port (translate Python dataclasses to C# records) |
| Workflow executor (Kahn) | `Beep.AI.MLStudio/Beep.AI.MLStudio/app/services/workflow/executor.py:25-410` | Port |
| Health summary state machine | `Beep.AI.MLStudio/Beep.AI.MLStudio/app/models/workflow.py:121-131` | Port |
| Experiment lineage snapshot diff | `Beep.AI.MLStudio/Beep.AI.MLStudio/app/models/experiment.py:50-70, 251-292` | Port |
| `@admin_required` decorator | `Beep.AI.Server/Beep.AI.Server/app/utils/permissions.py:185` | Adopt (translate to ASP.NET policy) |
| Singleton `SecuritySettingsService` | `Beep.AI.MLStudio/Beep.AI.MLStudio/app/services/admin/security_settings.py:38` | Adopt pattern, replace with typed settings service |

## 4. Anti-patterns to avoid

| Anti-pattern | Source reference | Mitigation |
|---|---|---|
| JSON as source of truth for behaviour | Beep.AI.Server AGENTS.md | Typed settings service; JSON is registry state only |
| Massive service files (1000-2000 lines) | `Beep.AI.Community/Beep.AI.Community/app/services/competition_service.py` | Per BeepWeb AGENTS.md: 300-500 line cap |
| Inline `<script>` / `<style>` in templates | Beep.AI.Community `templates/base.html` | JS in `wwwroot/js`, CSS in `wwwroot/css` only |
| Scoring service executes user-uploaded Python | `Beep.AI.Community/Beep.AI.Community/app/services/scoring_service.py:80-138` | Do not execute user scripts; trusted scorer plugins only |
| Two competing theme systems | Beep.AI.Community `BrandingConfig` vs `themes/config.py` | One `IThemeProvider` and one `BrandingConfig` |
| Hardcoded paths in services | Beep.AI.Community | All paths from `IOptions<KocOptions>` |
| Bare `except:` clauses | Beep.AI.Community | Specific exception types or `Exception` with logging |
| `socketio.run(allow_unsafe_werkzeug=True)` | Beep.AI.MLStudio `run.py:195` | SignalR over the configured Kestrel transport |

## 5. Pinned package versions (verified 2026-07-17)

See `references/TECHNOLOGY_MATRIX.md` for full pinning.

## 6. Out-of-scope and deferred

See `references/RISKS_AND_DEFERRED_SCOPE.md` for the full list. Headlines:

- Multi-tenant support, external community, public marketplace.
- Public competition leaderboards.
- Notebook execution.
- Arbitrary user-uploaded script execution in Worker.
- On-prem deployment (documented as future option).
- Cross-region failover outside Kuwait sovereign boundary.

## 7. Acceptance gate

- Every required feature maps to a stage, owner project, entity, endpoint, and acceptance test.
- Every decision above is reviewed and accepted by stakeholders.
- References catalog is complete and cross-linked.
