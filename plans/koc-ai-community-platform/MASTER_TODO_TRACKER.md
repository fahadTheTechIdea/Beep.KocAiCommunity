# Beep.KocAiCommunity — Master Todo Tracker

**Plan folder:** `plans/koc-ai-community-platform/`
**Status:** 🟢 BUILDING — foundation/auth/data/API/shell/competitions/learning + engagement (05b) + Worker queue (10) + experiment tracking (11) + model registry & inference (12) shipped and compiling; enterprise/admin half in progress
**Status audit:** 2026-07-19 — verdicts below verified against actual code (`src/`). Solution builds `-warnaserror` clean; 123 unit + 89 integration tests pass. Every feature phase (00–14a) is now DONE or MOSTLY DONE; only deployment-time ops (Azure/IaC/DR/perf, live connector adapters) remain. This session implemented 05b, 06, 07, 07a, 08, 09, 12, 14, 14a, 15 + a Studio IA consolidation (designer + registry unified under a "Studio" nav group; registry workflows open in the designer).
**Goal:** Build a dedicated, single-tenant, internal platform for Kuwait Oil Company (KOC) that trains and familiarizes employees with AI/ML through guided learning tracks and internal, Kaggle-style competitions, with management supervision via org-scoped rollups. Built on .NET 10, MudBlazor 9, and ML.NET 5. Internal KOC application — not a commercial product.

**UX north star (added 2026-07-19):** the platform must feel **community-first and fun** — employees earn Barrels (bbl), climb an O&G career ladder, collect badges from the shipped O&G icon library, give kudos, and see Team-vs-Team leaderboards. See Phase 05b.

---

## Phase checklist

- [x] **Phase 00 — Research and architecture decisions** (`00_RESEARCH_AND_ARCHITECTURE_DECISIONS.md`) — ✅ DONE
  - [x] Research findings, precedents, Z.Blazor.Diagrams / `IMonitor` / Entra / Aspire evaluations, pinned versions, KOC focus decisions, out-of-scope — all documented in the phase file

- [x] **Phase 01 — Solution foundation** (`01_SOLUTION_FOUNDATION.md`) — ✅ DONE
  - [x] Solution + all 16 src / 5 test projects created
  - [x] `Directory.Build.props`, `Directory.Packages.props`, `global.json`
  - [x] Nullable, analyzers, deterministic builds, warnings-as-errors
  - [x] Aspire AppHost wires Api/Worker/Web + SQLite-dev/SqlServer-publish
  - [x] `ServiceDefaults` with health checks, OpenTelemetry, resilience
  - [x] Unit, integration, component, architecture, e2e test projects exist
  - [x] Gate: restore/build pass (verified 2026-07-19)

- [x] **Phase 02 — Entra ID, security, and RBAC** (`02_ENTRA_ID_SECURITY_AND_RBAC.md`) — ✅ DONE
  - [x] Microsoft.Identity.Web for Web OIDC + API JWT with dev fallback (`ServiceDefaults/Security/SecurityExtensions.cs`)
  - [x] Position + function roles and policies (`KocRoles.cs`, `KocPolicies.cs`)
  - [x] Org hierarchy `OrgUnit` tree + `OrgMembership`
  - [x] `IOrgScopeResolver` + `IVisibilityEvaluator`
  - [x] Resource permissions (`UserEntityPermission.cs`)
  - [x] Bootstrap (`DevOrgSeeder.cs`) and audit envelope (`AuditEnvelopeService.cs`)
  - [ ] Register real KOC Entra applications (deployment-time task)

- [x] **Phase 03 — EF Core data and artifact storage** (`03_EF_CORE_DATA_AND_ARTIFACT_STORAGE.md`) — ✅ DONE (1 gap)
  - [x] `KocDbContext` + 11 entity configurations
  - [x] SqlServer/SQLite provider selection, dual migration sets
  - [x] `IArtifactStore` + `LocalFileArtifactStore` + `ArtifactService` (limits, allowlists)
  - [x] KOC data classification (`KocDataClassification.cs`)
  - [ ] Azure Blob artifact provider (gap — local FS only today)

- [x] **Phase 04 — API contracts and real-time events** (`04_API_CONTRACTS_AND_REALTIME_EVENTS.md`) — ✅ DONE (2 gaps)
  - [x] `/api/v1` Minimal API surface (11 endpoint groups)
  - [x] Problem Details, OpenAPI, pagination, rate limiter
  - [x] SignalR `LeaderboardHub` + transactional outbox (`OutboxWriter` → `OutboxDispatcher`)
  - [ ] ETags (gap)
  - [ ] Idempotency keys (gap)

- [x] **Phase 05 — MudBlazor shell and setup** (`05_MUDBLAZOR_SHELL_AND_SETUP.md`) — ✅ DONE
  - [x] KOC-branded shell (`MainLayout`, `NavMenu`, `NotificationBell`), 12 routed pages
  - [x] `Ui.Shared` theme/branding (`KocTheme.cs`, `KocBrand.cs`, blueprint CSS, 236 O&G icons)
  - [ ] Command palette (Ctrl+K) — not verified in code
  - [ ] Setup diagnostics page — not verified in code
  - Note: `Ui.Studio` / `Ui.Community` / `Ui.Admin` are empty scaffolds; live UI sits in `Web` (acceptable for now, revisit at 14a)

- [x] **Phase 05b — Community engagement and gamification** (`05b_COMMUNITY_ENGAGEMENT_AND_GAMIFICATION.md`) — ✅ DONE (2026-07-19)
  - [x] `UserProfile` (avatar from O&G icon library, bio, skills) + `/profile` + `/profile/{id}` page with XP ring and badge wall
  - [x] Barrels (bbl) XP ledger (`XpEvent`), idempotent `AwardXpAsync`, daily caps
  - [x] O&G career ladder levels (`KocLevels`: Roustabout → Chief Geoscientist)
  - [x] Badge catalog + `BadgeRules` + `EngagementSeeder` (icons from `Ui.Shared/wwwroot/icons/`)
  - [x] Kudos (peer recognition, curated emoji, daily cap of 10, real-time notification)
  - [x] Streaks (flame in app bar) + streak badges
  - [x] Individual + **team (org-unit) leaderboards** (average bbl per member, week/month/all) — Community "Leaderboards" tab
  - [x] Org-scoped activity feed — Community "Activity" tab (visibility-filtered)
  - [x] XP hooks in `LearningService`, `CompetitionService`, `CommunityService` (side-effect-safe)
  - [x] Real-time confetti celebrations via `LeaderboardHub` user-group + `celebrations.js` (reduced-motion safe)
  - [x] Personalized Home greeting (incl. Arabic variant) + app-bar Barrels/streak `StandingChip`; medal-tinted top-3 leaderboard rows
  - [x] Dual-provider `AddEngagement` migrations (SQLite + SqlServer); badge seeding wired into API startup
  - [x] Gate met: lesson completion → bbl + first-barrel badge + confetti + leaderboard row; kudos pay recipient in real time; activity feed visibility-scoped. 22 unit + 5 integration tests pass.
  - [~] Deferred within phase: Compete-page medal/rank-change animation polish; Spotlight carousel on Home; `team-player` badge auto-award (needs a team-challenge mechanic); printable certificates

- [x] **Phase 06 — Collaboration and discussions** (`06_COLLABORATION_AND_DISCUSSIONS.md`) — ✅ DONE (2026-07-19)
  - [x] Discussions + replies with org-scoped visibility (`Discussion.cs`, `CommunityService`, `DiscussionEndpoints`, `Community.razor`)
  - [x] Notifications + bell (`NotificationService`, `NotificationBell.razor`)
  - [x] Emoji reactions on discussions + replies (curated set, toggle, `Reaction` entity, `ReactionBar.razor`)
  - [x] Moderation: lock (blocks replies), pin (sorts top), soft-delete — moderator (PlatformAdmin or org leader) or author; all audited
  - [x] Mentions (@user) resolved to KOC profiles + `mention` notification + KOC-only autocomplete (`/community/mention-candidates`)
  - [x] Attachments via `IArtifactService` (upload/list/download, visibility-scoped, `Internal` classification)
  - [x] Dual-provider `AddCommunityInteractions` migrations; 5 integration tests pass
  - [x] Profiles/skills/interests/avatars + activity feed → delivered in Phase 05b (`UserProfile`, `ActivityEvent`)
  - [~] Deferred: server-side malware scanning of attachments (documented follow-up); in-browser attachment download link assumes same-origin/gateway

- [~] **Phase 07 — Datasets, projects, and collaboration** (`07_DATASETS_PROJECTS_AND_COLLABORATION.md`) — 🟢 MOSTLY DONE (2026-07-19)
  - [x] Dataset entity with visibility + classification (`Dataset.cs`, `DatasetService`)
  - [x] ~~Projects (`Project.cs`, `ProjectService`)~~ — **retired 2026-07-20**; merged into the versioned Workflows registry (a workflow optionally targets a competition via `CompetitionId`). See tracker item 19.
  - [x] Org-scoped visibility on datasets and projects
  - [x] Immutable dataset versions + files (`DatasetVersion`/`DatasetFile`, `DatasetContentService`); publish freezes, new upload opens a draft
  - [x] CSV schema inference + reproducible sampled profiling (`CsvProfiler` → `DatasetSchemaColumn`/`DatasetProfile`/`DatasetProfileColumn`) + licenses (`LicenseSpdxId`)
  - [x] File + URL import adapters; URL import SSRF-guarded (`UrlImportGuard`)
  - [x] Classification enforced on download (Confidential/Restricted → owner or admin only)
  - [x] `/datasets/**` versioning endpoints + typed client + `DatasetVersionsDialog`; dual-provider `AddDatasetVersioning`; 11 unit + 3 integration tests
  - [~] Deferred: SQL import adapter (needs live read-only DB), project collaboration depth (members/roles/activity/templates), dataset→project AutoML import (Phase 11), Parquet, exhaustive profiling

- [~] **Phase 07a — KOC enterprise connectors** (`07a_KOC_ENTERPRISE_CONNECTORS.md`) — 🟢 MOSTLY DONE (2026-07-19)
  - [x] `IKocConnector`/`IKocConnectorFactory` abstractions + code-first `ConnectorCatalog` (6 connectors, per-connector default classification, auth modes, capabilities)
  - [x] `ConnectorInstance` + encrypted `CredentialVaultEntry` (via `ISecretProtector`) + `ConnectorHealthSnapshot`; dual-provider `AddConnectors` migration
  - [x] `ConnectorService`: instance CRUD, SSRF-guarded endpoints, credential vault (encrypted, never returned), test/schema/health-with-snapshot, audited
  - [x] `MockConnector` staging adapter (deterministic O&G schema); `/api/v1/connectors/**` (PlatformAdmin) + typed client + `/connectors` page
  - [x] 3 unit + 4 integration tests (admin-only, SSRF-blocked, secret never exposed in responses/audit)
  - [~] Deferred (deployment-time): six live adapters (PPDM/OpenWells/EcoSys/SAP/PI/ADLS), scheduled health-monitor hosted service, connector→dataset import with lineage, Key Vault refs

- [~] **Phase 08 — ML.NET runtime and node catalog** (`08_MLNET_RUNTIME_AND_NODE_CATALOG.md`) — 🟢 MOSTLY DONE (2026-07-19)
  - [x] ML.NET executor with real transforms + AutoML (`ML/MlPipelineExecutor.cs` ~740 lines, `AutoMlTrainer.cs`, `IMlRuntime.cs`)
  - [x] AutoML trials for binary/multiclass/regression
  - [x] Formal code-first `INodeDescriptor`/`NodeParameter`/`INodeRegistry` (`Application/ML/NodeCatalog.cs`, `MlNodeRegistry`) — kept in sync with `WorkflowCompiler.KnownKinds`
  - [x] O&G task catalog (`MlTaskCatalog`: binary/multiclass/regression supported; anomaly/forecasting roadmap) + O&G-flavored node descriptions
  - [x] Split-before-fit guard (`FeaturizationGuard`) enforced on workflow publish; fixed-seed determinism test
  - [x] `/api/v1/ml/nodes|tasks|workflows/featurization-check` endpoints + typed client + `/nodes` catalog page; 9 unit + 3 integration tests
  - [~] Deferred: executable anomaly/forecasting handlers, full `IMlTaskHandler`/`INodeExecutor` per-node split (executor runs graphs today), recommendation/TorchSharp

- [x] **Phase 09 — Workflow designer, compiler, versioning** (`09_WORKFLOW_DESIGNER_COMPILER_AND_VERSIONING.md`) — ✅ DONE (2026-07-19)
  - [x] Z.Blazor.Diagrams designer (`WorkflowDesigner.razor` ~618 lines, `MlNode.cs`, `NodeCatalog.cs`)
  - [x] `WorkflowDefinition` JSON contract + compiler with cycle detection/topo sort (`WorkflowCompiler.cs`)
  - [x] Immutable versions + drafts + publish (compiler-gated) + archive (`Workflow`/`WorkflowVersion`, `WorkflowVersionService`); published graphs frozen, edits open a new draft
  - [x] Canonical snapshot hashing (`WorkflowSerializer`, SHA-256) — provenance + loss-free round-trip
  - [x] Import/export (`koc-workflow-export` envelope) + O&G templates (`WorkflowTemplateSeeder`, `WorkflowTemplateService`)
  - [x] `/api/v1/workflows/**` + `/workflow-templates` endpoints + typed client + `/workflows` management page
  - [x] Dual-provider `AddWorkflows` migrations; 4 unit + 4 integration tests pass
  - [~] Deferred: 200-node browser-scale gate (perf benchmark) and dagre/ELK auto-layout

- [x] **Phase 10 — Worker execution and orchestration** (`10_WORKER_EXECUTION_AND_RUN_ORCHESTRATION.md`) — ✅ DONE (2026-07-19)
  - [x] EF-backed durable job queue (`EfJobQueue`) with atomic `ExecuteUpdate` claim (single-owner, provider-portable)
  - [x] Heartbeat lease renewal; exponential backoff retries → dead-letter; cooperative cancellation; expired-lease crash recovery
  - [x] `JobProcessor` (dispatch + heartbeat + terminal states) and `JobExecutionService` (N concurrent loops, graceful drain, SQLite=1)
  - [x] Run progress via outbox → `LeaderboardHub` `run:{id}` group; `ModelTrainingJobHandler` runs real AutoML out-of-band
  - [x] `/api/v1/runs` create/get/list/cancel/logs/attempts (owner/PlatformAdmin authz) + typed client + `/runs` live monitor page
  - [x] Dual-provider `AddJobs` migrations
  - [x] Gate met: runs survive restart (expired-lease reclaim), cancellation works, duplicate claims prevented, progress via SignalR. 16 unit + 5 integration tests pass.
  - [~] Deferred: SQL Server multi-worker concurrency wired but tested at 1; memory-limit hint not enforced; Worker reuses API's outbox dispatcher; run-detail component test

- [x] **Phase 11 — Experiment tracking and evaluation** (`11_EXPERIMENT_TRACKING_AND_EVALUATION.md`) — ✅ DONE (2026-07-19)
  - [x] `Experiment`/`Run`/`RunMetric`/`RunParameter` entities + dual-provider `AddExperiments` migrations
  - [x] Non-blocking trial capture: AutoML `progressHandler` → `BoundedMetricChannel` (TryPublish never blocks) → batched drain → `IExperimentSink`
  - [x] `IExperimentSink` fan-out (EfExperimentSink default; swappable — MLflow-adapter contract) + best-run selection + comparison + lineage snapshots (hyperparams/env/dataset hash)
  - [x] `experiment.train` job handler (runs out-of-band via Phase 10) + `/api/v1/experiments/**` endpoints + typed client + `/experiments` UI (runs, best star, favorites, compare, SVG trial chart)
  - [x] Gate met: multiple AutoML trials persist live metrics; comparison reproducible; lineage captured; `IMonitor` non-blocking; sink swappable. 9 unit + 4 integration tests pass.
  - [~] Deferred: MLflow REST adapter (contract only); confusion-matrix/ROC/residual/forecast/feature-importance viz; parent/child run trees

- [x] **Phase 12 — Model registry and inference** (`12_MODEL_REGISTRY_AND_INFERENCE.md`) — ✅ DONE (2026-07-19)
  - [x] Registry with lifecycle states, promote/rollback/deploy (`ModelRegistry.cs`, `ModelRegistryService.cs`, `ModelEndpoints.cs`)
  - [x] Training captures + persists the winning model as a governed artifact (`TrainAndCaptureAsync`, `ModelRun.ModelArtifactId`) + drift baseline (`FeatureStatsJson`)
  - [x] Thread-safe, hot-reloadable prediction pool (`IPredictionPool`/`AutoMlPredictionPool`) — dynamic-schema scoring (typed `PredictionEnginePool<T>` can't handle arbitrary CSV schemas); evicted on retire/rollback
  - [x] Protected online + batch inference (`IInferenceService`) with per-call `ModelInferenceLog` (latency/outcome) and owner/admin authz on non-production versions
  - [x] Drift comparison endpoint (batch feature means vs training baseline); `/infer`, `/infer/batch`, `/inference-logs`, `/drift` endpoints + typed client + `Predict` dialog on `Models.razor`
  - [x] Dual-provider `AddInference` migrations
  - [x] Gate met: model moves experiment→registry→inference and rolls back; promotion needs two approvals; inference logs latency/outcome; classification carried on the model artifact. 4 new tests (2 unit + 2 integration).
  - [~] Deferred: typed `PredictionEnginePool<T>` for fixed O&G templates; continuous drift monitoring; model signing via KOC CA/KMS; multi-page `Studio/Models/*` structure

- [x] **Phase 13 — Competitions and leaderboards** (`13_COMPETITIONS_AND_LEADERBOARDS.md`) — ✅ DONE
  - [x] Internal Kaggle-style competitions with org-scoped visibility (`CompetitionEntities.cs`, `CompetitionService.cs`)
  - [x] Hidden answer key, quotas, concealed-final reveal (`RevealUtc`)
  - [x] Trusted scorers (`AccuracyScorer`, `RmseScorer`, `ScorerRegistry`), seeding
  - [x] Real-time leaderboard (`LeaderboardHub`), supervisor rollups (`SupervisionService`)
  - [ ] Fun layer (medals, rank animations, team standings) → Phase 05b

- [x] **Phase 13a — Learning tracks and upskilling** (`13a_LEARNING_TRACKS_AND_UPSKILLING.md`) — ✅ DONE
  - [x] Tracks/lessons/enrollment/progress/completion (`LearningEntities.cs`, `LearningService.cs`, `LearningSeeder`)
  - [x] Starter tracks seeded; learn ↔ compete tie-in (`RecommendedTrackId`)
  - [x] Supervisor rollups
  - [ ] XP + certificates on completion → Phase 05b (XP) / deferred (certificates)

- [~] **Phase 14 — O&G templates, domain admin, and help** (`14_INDUSTRY_TEMPLATES_ADMIN_AND_HELP.md`) — 🟢 MOSTLY DONE (2026-07-19)
  - [x] O&G template taxonomy (4 subdomains: upstream/midstream/downstream/hse) via existing `WorkflowTemplateSeeder` + `?domain=` filter
  - [x] In-app help + FAQ: code-first `HelpCatalog`/`IHelpService` (browse/search/read), `/api/v1/help/**` endpoints, `/help` page with Markdown rendering + search
  - [x] Per-domain admin delivered via existing surfaces (`/admin` console, competition admin, moderation, `/admin/overview`)
  - [x] 4 unit + 2 integration tests
  - [~] Deferred: dedicated `IndustryTemplateDefinition` entity, interactive step-through tutorials, admin-authored help content, retention/cleanup policies (Phase 15 ops)

- [~] **Phase 14a — Platform admin, settings, audit** (`14a_PLATFORM_ADMIN_SETTINGS_AND_AUDIT.md`) — 🟢 MOSTLY DONE (2026-07-19)
  - [x] Audit plumbing (`AuditEnvelopeService`, `AdminAuditLog`, outbox, `RequirePlatformAdmin` policy)
  - [x] Typed settings (code-first `SettingsCatalog` + `SettingValue`; secrets encrypted via `ISecretProtector`/Data Protection, masked in responses + audit, versioned)
  - [x] Feature flags (boolean + stable-hash rollout %) with audit
  - [x] Admin dashboard (live counts + recent audit + health) + audit query
  - [x] `/api/v1/admin/**` all behind `RequirePlatformAdmin` (403 to non-admins) + typed client + `/admin` console (Dashboard/Settings/Flags/Audit tabs); scaffold moved to `/admin/overview`
  - [x] Dual-provider `AddAdmin` migrations; 4 unit + 8 integration tests pass
  - [~] Deferred: DB-backed platform roles/permission grid + first-admin bootstrap (auth is Entra-claim based), sessions, background health monitor, maintenance tasks, email/broadcast, classification editor

- [~] **Phase 15 — Testing, hardening, deployment, migration** (`15_TESTING_HARDENING_DEPLOYMENT_AND_MIGRATION.md`) — 🟡 PARTIAL
  - [x] Real unit + integration suites; CI workflow; docker-compose + Dockerfiles
  - [x] OWASP-aligned security suite (`SecurityTests`: anonymous→401, IDOR/cross-team invisibility, privilege escalation, path-traversal) + SSRF/secret-masking/admin-403 checks
  - [x] Migration-chain test (`MigrationChainTests`: full SQLite chain applies, no pending, tables queryable)
  - [x] Architecture tests lock layering (Domain/Application free of EF/ASP.NET/MudBlazor/ML)
  - [ ] Performance/load benchmark suite
  - [ ] Azure Kuwait deployment, Bicep IaC, Managed Identity, Key Vault (deployment-time)
  - [ ] Backup/restore/DR runbooks, read-only Python-metadata importer (deployment-time)

## Recommended build order (from here)

1. ~~Phase 05b — engagement/gamification~~ ✅ DONE (2026-07-19)
2. ~~Phase 10 — Worker durable queue~~ ✅ DONE (2026-07-19)
3. ~~Phase 11 — Experiment tracking~~ ✅ DONE (2026-07-19)
4. ~~Phase 12 — Model registry inference~~ ✅ DONE (2026-07-19)
5. ~~Phase 06 — Collaboration completion~~ ✅ DONE (2026-07-19)
6. ~~Phase 09 — Workflow versioning~~ ✅ DONE (2026-07-19)
7. ~~Phase 14a — Platform admin (core)~~ ✅ DONE (2026-07-19)
8. ~~Phase 07 — Dataset depth~~ ✅ DONE (2026-07-19)
9. ~~Phase 08 — Node catalog + featurization guard~~ ✅ DONE (2026-07-19)
10. ~~Phase 15 — Security + migration-chain hardening~~ ✅ (test suites; deployment/IaC/DR still deferred)
11. ~~Phase 14 — O&G template taxonomy + in-app help/FAQ~~ ✅ DONE (2026-07-19)
12. ~~Phase 07a — Enterprise connectors (abstractions + vault + mock adapters)~~ ✅ DONE (2026-07-19)
13. ~~Studio IA consolidation~~ ✅ (designer + registry unified under a "Studio" nav group; registry → open in designer)
14. **All original feature phases complete.** Remaining = deployment-time only: Azure/Bicep/Key Vault, DR runbooks, perf benchmarks, Python importer, six live connector adapters.

## DuckDB integration initiative (plug-and-play node engine)

15. ~~Phase 16 — Pluggable node engine~~ ✅ DONE (2026-07-20) — `IPipelineNodeHandler` + `PipelineContext` + `PluginNodeRegistry` + `PluginNodeExecutor`; all ~30 ML nodes migrated behaviour-preserving; monolithic executor deleted. See `16_PLUGGABLE_NODE_ENGINE.md`.
16. ~~Phase 17a — DuckDB engine dependency proven~~ ✅ DONE (2026-07-20) — `DuckDB.NET.Data.Full`, `DuckDbSession`, probe tests (native engine loads + CSV round-trip).
17. ~~Phase 17b — Engine crossing + first DuckDB nodes~~ ✅ DONE (2026-07-20) — lazy Duck↔ML crossing via CSV; DuckDB is the data-prep front-end (must precede ML nodes); `sql`, `sql-filter`, `group-by`, `sort`, `distinct` handlers; `IPipelineExecutor` gains optional secondary datasets. See `17_DUCKDB_ENGINE.md`.
18. ~~Phase 18 — Remaining DuckDB nodes + secondary datasets~~ ✅ DONE (2026-07-20) — `join-dataset`, `union-dataset`, `pivot`, `limit`, `summarize`; secondary-dataset resolution in the callers; uniform `PipelineTable` (CSV-backed) contract so DuckDB and ML nodes share one data interface in either order. See `18_DUCKDB_NODES.md`.
20. ~~API-driven designer palette + Dataset picker~~ ✅ DONE (2026-07-20) — the Studio canvas palette + property inspector now render from the live backend registry (`GET /api/v1/ml/nodes` via `Api.GetMlNodesAsync()`), so the DuckDB nodes (`sql`, `sql-filter`, `group-by`, `sort`, `distinct`, `join-dataset`, `union-dataset`) appear automatically. `NodeParameterType.Dataset` params render a dataset picker, so join/union can select a second dataset. Hardcoded `Web/Diagrams/NodeCatalog.cs` retired; replaced by presentation-only `NodeVisuals` (kind/category → icon + colour). Completes the Phase 18 UI remainder.

19. ~~Projects → Workflows merge~~ ✅ DONE (2026-07-20) — the `Projects` concept is retired; the versioned **Workflows** registry is the single home for pipelines. A workflow optionally carries a `CompetitionId` (replacing the old `Project.CompetitionId`); the "New workflow" dialog picks an optional competition, and a competition-targeting workflow submits its pipeline from the canvas. `Project` domain/service/endpoints/DTOs/dialog deleted; dual-provider `MergeProjectsIntoWorkflows` migration (drops `Projects`, renames `Workflows.ProjectId` → `CompetitionId`). The `/workflow` designer is always registry-backed (no more project-browse mode); `/workflow` with no id redirects to `/workflows`.

## Admin RBAC — competition-creator grants, org codes, user profiles

21. **Phase 1 — model + enforcement** ✅ DONE (2026-07-21) — `OrgUnit.Code` (e.g. `AX01`);
    `UserProfile` extended with `Email`/`CompanyId`/`DepartmentId` (all org **codes** — a user's
    single `DepartmentId` code IS their org placement, no separate `OrgUnitId` FK); new
    `CompetitionCreatorGrant` (per-user, `MaxScope`) with dual-provider migrations
    (`AddRbacUserProfiles` + `SimplifyUserProfileDepartment`). Competition creation is now **granted-only**:
    `CompetitionService.CreateAsync` requires a platform-admin caller or an active grant whose
    max scope covers the requested audience (else `CompetitionAccessException` → HTTP 403);
    `GetMaxCreateScopeAsync` feeds `MeResponse.MaxCompetitionScope`, which gates the Host button
    and caps the create wizard's scope picker. `DevOrgSeeder` seeds unit codes, user profiles,
    and per-persona grants. New integration tests: ungranted → 403, above-cap → 403, at-cap →
    200, platform-admin any-level → 200.
22. **Phase 2 — admin RBAC console** ✅ DONE (2026-07-21) — `IAccessAdminService` (list users,
    upsert profile with company/dept-code derivation from the picked org unit, set/revoke
    competition grant, set unique org-unit code; every mutation audited via `IAuditEnvelope`) +
    six `RequirePlatformAdmin` endpoints under `/api/v1/admin` + a **"RBAC / Users"** tab in
    `Admin.razor` (per-user "Can create competitions" level picker, email + department editors,
    and an org-unit code editor) + `KocApiClient` methods. New integration tests: grant→create
    allowed / above-cap 403 / revoke→403, org-code set + uniqueness, department→derived codes,
    and non-admin 403 on the new endpoints.

## WinForms Desktop Studio (offline-first designer + online competitions)

Reuse the Blazor Studio designer inside a WinForms shell via **BlazorWebView**. The
designer + node catalog + pipeline **run** work **fully offline** on a local in-process
engine (`PluginNodeExecutor` + ML.NET + DuckDB against local CSVs); only competitions
call the API. Decisions & architecture: `19_WINFORMS_DESKTOP_STUDIO.md`.

23. **Stage 1 — shared `Beep.KocAiCommunity.Client`** ✅ DONE (2026-07-21) — new `net10.0`
    library holds `IKocApiClient`/`KocApiClient`/`DevIdentity`/`DevIdentityHandler`/
    `RealtimeOptions` + `AddKocHttpClient(baseUrl)`; Web references it and calls the helper.
    Behaviour-preserving (255 tests green). → `19a_…`
24. **Stage 2 — Studio UI → `Ui.Studio` RCL** ✅ DONE (2026-07-21) — `WorkflowDesigner`/
    `Workflows`/`CreateWorkflowDialog`/`RunWorkflowDialog`/`NodeVisuals`/`MlNode` moved from
    Web into the `Ui.Studio` RCL (references Client + Z.Blazor.Diagrams); the Web router
    already lists the RCL assembly, so `/workflow` + `/workflows` render unchanged. 255 green.
25. **Stage 3 — WinForms BlazorWebView shell (thin client)** 🟢 BUILT (2026-07-21) — new
    `net10.0-windows` `Beep.KocAiCommunity.WinForms` (WebView.WindowsForms 10.0.80): `Program`
    (ServiceCollection + `AddWindowsFormsBlazorWebView` + `AddMudServices` + `AddKocHttpClient`),
    `MainForm` (full-bleed `BlazorWebView`), `Shell` (Mud providers + `Router` over the Ui.Studio
    assembly), `DesktopLayout`, `Index` launcher, and `wwwroot/index.html` loading MudBlazor +
    Z.Blazor.Diagrams + koc-blueprint assets. Solution builds `-warnaserror` (MSB3277 WebView2/WPF
    WindowsBase conflict demoted to a message), 255 tests green. **Pending manual smoke test:**
    launch the app with the API running and confirm the designer renders in WebView2. → `19b_…`
26. **Stage 4 — local execution engine** ✅ DONE (2026-07-21) — new `Beep.KocAiCommunity.Desktop.Local`:
    `RemoteFallbackKocApiClient` (129 delegating methods → HTTP) + `LocalKocApiClient` overriding
    the Studio surface — node catalog from `PluginNodeRegistry`, datasets from local CSVs
    (`LocalDatasetStore` with a stable id index), pipeline **runs** via in-process
    `PluginNodeExecutor` (+ secondary datasets for join/union), workflow registry as local JSON
    (`LocalWorkflowStore`). `AddKocLocalStudio(apiBaseUrl)` wires the engine; the WinForms host now
    uses it. New `LocalStudioTests`: catalog (ML + DuckDB), registry round-trip, and an **offline
    end-to-end run** (no server). 258 tests green. → `19c_…`
27. **Stage 5 — competitions bridge** ✅ DONE (2026-07-21) — competition browse/submit/leaderboard
    flow through the delegating HTTP fallback (`RemoteFallbackKocApiClient`). New desktop
    **Competitions** screen (list + leaderboard, offline-graceful "connect to the KOC network"
    with retry) and **Settings** page (API base URL persisted to `settings.json`, live persona
    switch via `DevIdentity`); `AppSettings` load/save; nav in `DesktopLayout`. The designer's
    existing Submit-to-competition card posts the local `WorkflowDefinition` via the same fallback.
    Solution builds `-warnaserror`, 258 tests green. → `19d_…`
28. **Stage 6 — packaging + docs** ✅ DONE (2026-07-21) — app metadata (`KocStudio.exe`, product,
    v1.0.0); `settings.json` first-run config; `dotnet publish -r win-x64` verified — output bundles
    `KocStudio.exe`, the host page, all `Microsoft.ML.*` + `DuckDB.NET` assemblies and `runtimes/win-x64`
    natives (incl. `WebView2Loader.dll`). README gains a **Desktop app (offline Studio)** section + the
    two new projects in the solution-layout; memory note `winforms-desktop-studio.md`. The RID is passed
    at publish time so the solution build stays cross-platform. **WinForms initiative complete.**

## Competition Arena UI/UX revamp (landing + competitions)

Decisions: featured-competition hero on Home; full Kaggle-style pages per competition
(`/compete/{id}`); bold arena visuals (podiums, countdowns, live pulses, rank movement) inside the
KOC blueprint theme. No DB schema change anywhere.

29. ~~Stage 1 — DTO enrichment~~ ✅ DONE (2026-07-22) — `CompetitionDto` + ParticipantCount/
    SubmissionCount/HostName/QuotaPerDay/MetricName/HigherIsBetter/CreatedUtc (append-only,
    computed via `GetStatsAsync` in one grouped query + `IScorerRegistry`); shared
    `CompetitionRewards` constants wired into `XpSources` so prize copy can't drift.
30. ~~Stage 2 — arena vocabulary~~ ✅ DONE (2026-07-22) — koc-blueprint.css Arena section +
    shared components: `CountdownTimer`, `RankMedal`, `PodiumBlock`, `CompetitionCard`,
    `LiveBoard` (client-side ▲/▼/NEW rank movement), `CompetitionDisplay`; 9 bUnit tests.
31. ~~Stage 3 — /compete/{id}~~ ✅ DONE (2026-07-22) — full-width arena page (banner + Overview/
    Data/Leaderboard/Submissions/Host tabs, "What you can win" rewards strip, `?tab=` deep link);
    SignalR joins `competition:{id}` and re-joins on reconnect; notification LinkUrls deep-link.
32. ~~Stage 4 — /compete grid~~ ✅ DONE (2026-07-22) — browse grid of `CompetitionCard`s with
    status/task filters + a spotlight card (single fetched podium); hosting lands on
    `/compete/{id}?tab=host`; no per-card board fetches; 748→~170 lines.
33. ~~Stage 5 — Home hero~~ ✅ DONE (2026-07-22) — landing leads with the featured competition
    (nearest reveal): countdown, live top-3 podium, stats, prize strip, ENTER THE ARENA;
    champions mini-card + live-competitions section run on the enriched DTO with deep links.
34. ~~Stage 6 — polish + docs~~ ✅ DONE (2026-07-22) — user guide + tracker updated; visual
    verification of the new pages via Playwright.

## Demo-environment disclaimer

35. ~~Bilingual demo notice~~ ✅ DONE (2026-07-23) — when seeded demo data is present, a one-time
    English/Arabic "Demonstration environment" overlay greets every visitor so sample colleagues,
    competitions, and results aren't mistaken for real KOC records. New anonymous `GET /api/v1/meta`
    (`PlatformMetaDto{DemoMode, DemoDataSeeded}`, reusing `IDemoDataService` + `SecurityExtensions`);
    `DemoDisclaimer.razor` in `MainLayout` gates on `DemoDataSeeded`, dismissed per browser session
    (`sessionStorage`); hidden on production Entra/Windows-SSO or once unseeded. Integration test
    covers the anonymous endpoint + seed/unseed toggle; user & admin guides + help visuals updated.

## Environment awareness & production hardening

36. ~~Reliable dev/prod detection + fail-fast guard~~ ✅ DONE (2026-07-23) — immutable binaries:
    `KocHostEnvironment.Resolve()` (ServiceDefaults) honours explicit `ASPNETCORE_ENVIRONMENT`/
    `DOTNET_ENVIRONMENT`, else infers `Development` from `appsettings.Development.json` presence
    (excluded from prod publishes via `CopyToPublishDirectory=Never`); wired into API + Web via
    `WebApplicationOptions.EnvironmentName`. `KocProductionPreflight` throws at startup when a Production
    host is misconfigured (SQLite / Seed / DevAuth / no real auth). Dev persona switcher hidden outside
    Development (`MainLayout` gates on `IHostEnvironment.IsDevelopment()`). User Secrets ids added to
    API + Web. `DEPLOYMENT.md` gains an environment-resolution note + a DB credentials matrix
    (dev = User Secrets; on-prem = Integrated Security/gMSA; Azure = Managed Identity + Key Vault) and
    "how to change the connection string per environment". Best practice per MS Learn (.NET 10).

## Pipeline & scoring correctness (audit remediation)

37. ~~CSV codec + scoring correctness~~ ✅ DONE (2026-07-26) — end-to-end audit of nodes →
    executor → scoring. Fixed: shared RFC-4180 `KocCsv` codec (Application/Common) replacing naive
    `Split(',')`; id-aligned robust scorers with boolean-convention tolerance (fixes the latent binary
    `1/0` zero-score bug incl. Titanic); `TaskType`↔`ScorerCode` guard; answer-key validation on upload;
    deterministic leaderboard tie-break; scorer/CSV property-test battery. Data flow: predict
    id↔prediction alignment now reads ids from the same replayed table (no `Math.Min` shear, fails loud
    on mismatch); `__fold` leakage guard — a split then a column-dropping SQL node fails loudly instead
    of silently training on test rows. **Phase D (2026-07-26, lower-severity follow-ups):** DuckDB engine
    made deterministic (`SET threads TO 1` + `preserve_insertion_order`) and the `sort` node appends every
    column as a total-order tie-break, so `ORDER BY` ties are reproducible run-to-run; runtime node
    validation (`PluginNodeExecutor.ValidateNodeInputs`) fails a node loudly when a `Columns` param names a
    column absent from the table it operates on (join's column picker resolves against the joined dataset)
    or a `Dataset` param can't be loaded — instead of silently dropping/skipping; binary submissions now
    echo the training label's own token convention (`1/0`, `yes/no`) via `MlModelOps.BinaryLabelTokens`
    rather than a hardcoded `true/false`. 4 new unit tests (161 unit + 110 integration green). Deferred
    (design): the two execution engines (`workflow.run`/`/studio/workflows/run` run AutoML, not the
    graph), schema-carrying `PipelineTable`, RMSE-as-headline for regression.

## End-to-end pipeline & scoring audit (item 37 follow-on)

38. 🟡 **IN PROGRESS (2026-07-26)** — full audit of every node → workflow → executor → scoring path
    (`20_PIPELINE_AND_SCORING_AUDIT.md`). Confirmed two execution engines run the same
    `WorkflowDefinition` and disagree (competition scoring + `/studio/workflows/execute` run the real
    graph; `run`/`workflow.run`/`experiment.train`/`model.train` substitute AutoML and ignore the
    graph). **Shipped:** phase E (`151b3aa`) — id/label pinned to text across both crossings so
    numeric/zero-padded ids survive (was: `00123`→`123` collapsing the answer-key join = T2), DateTime
    CSV handling (H3), union-after-split fold fix (T4), and the last three naive-CSV sites routed
    through `KocCsv` (`AutoMlPredictionPool`/`AutoMlTrainer` drift baseline/`CsvProfiler` = M5/M6/M7);
    L10 (`82025c2`) — FastTree/FastForest pinned to one thread for reproducible training. **Open:** H2
    (schema-carrying `PipelineTable`, root-cause fix — patch ready, awaiting decision), T3 (compiler
    branch guard for the linear executor), S1–S5 (concealed-final board is cosmetic; answer-key swap
    doesn't rescore; accuracy boolean-folding collides for coded multiclass; accuracy/RMSE edge-case
    divergence; quota/leaderboard concurrency), T1 (route run/train through the node graph). Plus an
    assurance layer: a golden pipeline/competition correctness suite gated in CI.
    **Shipped since (2026-07-26 cont.):** H2 (`4b0d9f5`, schema-carrying `PipelineTable`); T3 (`c167586`);
    S3/S4 (`e0c705c`); S2 (`6a7d788`, answer-key rescore + concluded-key lock); fan-out-join duplicate-id
    guard (`0eff2e5`); S5 (`6f68df2`, leaderboard optimistic-concurrency token + retry); the data-class
    contract guards (`af6fe5f` — label/id integrity, target-leakage on compute-column, fold integrity on
    take-rows/sample-after-split, union label-missing, filter-rows typo); AutoML no-longer-trains-on-id
    (`5f53118`); **T1 path A** (`21be420` — interactive `/studio/workflows/run` executes the node graph,
    not AutoML). **T1 path B BLOCKED** — the durable `workflow.run` job produces a registerable ML.NET
    `.zip` model, which the graph can't yield (its preprocessing includes non-serializable DuckDB SQL
    steps); it stays on AutoML. Proper fix = serve inference by re-running the graph (`PredictAsync`), a
    rework of the inference path, tracked as future work. **S1 shipped** (`00f33e0`) — real Kaggle-style
    public/private holdout: each submission scored against a deterministic (stable-hash-halved) public and
    private partition of the answer key; live board = public (always visible), concealed final board =
    private (revealed at `RevealUtc`); dual-provider `AddHoldoutScores` migration. **Still open:** S5
    quota-TOCTOU residual (atomic per-user/day counter + migration) and the golden correctness suite. See
    `20_PIPELINE_AND_SCORING_AUDIT.md` for the full ledger.

## Node property panel completion (Studio designer UX)

39b. 🟢 **Unified parameter contract DONE (2026-07-26)** — replaced per-descriptor patching with **one
    typed `NodeParameter` class** the property panel binds to identically for every node (get/set through the
    same path, no per-field special-casing). Real type system — `Text/Number/Integer/Date/Boolean/Select/
    Columns/Column/Dataset` — each rendered with the right editor (numeric field with min/max, date picker,
    switch, lookup dropdown, column/dataset pickers). **Lookups carry objects** (`LookupOption{Value,Label,
    AppliesTo}`): the `algorithm` param hands the panel the trainer list from a single source of truth
    (`MlAlgorithms.All`) tagged by task, and the old `WorkflowDesigner.razor` `p.Name == "algorithm"` hack is
    gone — task filtering is now one generic `AppliesTo` rule. Contract flows end-to-end
    (`NodeParameter`→`NodeParameterDto`/`LookupOptionDto`→panel); server-side validation enforces the same
    types/ranges/lookup membership. Numeric params gained bounds (folds 2–10, clusters 2–20, bins 2–255,
    testFraction/fraction 0–1, …) + help text. Build + 192 unit + 3 node-endpoint integration tests green.

39. 🟢 **Phase 21a DONE (2026-07-26)** — expose **every** settable parameter of **every** node in the
    designer's property panel, with defaults shown on click. Spec: `21_NODE_PROPERTY_PANEL.md`;
    documentation is one file per node under `node-properties/` (37 files). **Root cause:** the inspector is
    descriptor-driven (`WorkflowDesigner.razor:104-140`) and complete for *declared* params, but the model
    nodes read hyperparameters the descriptor never declared — **descriptor↔executor drift** — so they were
    **hidden**: `train` exposed only `algorithm` while the executor reads
    `trees`(100)/`leaves`(20)/`learningRate`(0.2)/`l2` (`MlModelOps.cs:54-57`), `cross-validate` exposed only
    `folds`, and the `algorithm` list omitted `perceptron`/`naivebayes`. Hidden ⇒ locked to hardcoded
    defaults ⇒ no differentiation — a defect, not a design choice. **Fixed (descriptor completeness, no
    executor/`.razor` change):** declared the four hyperparameters + `algorithm` on `train`/`cross-validate`
    and extended the algorithm options (`MlModelHandlers.cs`) → the inspector renders them automatically,
    pre-filled with defaults; per-node docs updated. **Anti-drift guard shipped** (`NodePropertyDriftTests`):
    scans `MlModelOps` for every `Config` read and fails if any key isn't a declared descriptor parameter, so
    a hidden knob can't reappear; `NodeCatalogTests` asserts the full model-node parameter set. Unit suite 192
    green. **Pending:** Phase 21b (`VisibleWhen` for algorithm-conditional hyperparameters), 21c (help
    tooltips + range hints + reset-to-defaults).

40. 🟢 **22a–e DONE (2026-07-27)** — spec `22_NODE_REVISION_AND_ALGORITHMS.md`.
    **22d:** scalers (standardize/normalize/log-normalize/robust-scale/binning) + replace-missing gained an
    optional `columns` selector (blank = all numeric); `hash-encode` exposes `bits` (1–30); `one-hot` exposes
    `outputKind` (indicator/bag/key/binary) + a column selector; all honored by the executor
    (`NormalizeHandler.Restrict`). **22e (partial):** `join-dataset` gained a `joinType` dropdown
    (left/inner/right/full) and its key `on` is now a column picker. Proven via a configured-transforms golden
    test. UnitTests 215, ComponentTests 34 green. **Remaining:** visual builders (group-by aggregate builder,
    sort-key builder, sql-filter condition builder) — custom editors the descriptor panel can't express.
    **22e visual builders DONE:** `AggregateBuilder` (group-by function+column+name rows), `SortKeyBuilder`
    (column+direction rows), `SqlConditionBuilder` (column+operator+value rows, AND-joined) render in the panel
    for their node kind when columns are known, generate the SQL into the existing `aggregations`/`orderBy`/
    `where` config keys (executor unchanged), and keep the raw text field as an advanced fallback. Panel
    ComponentTests assert each builder appears for its kind and not others (35 total).
    **22b:** Train node now exposes `targetColumn`/`idColumn`/`task` (real column pickers + task select);
    competition mode pre-fills them from the competition and renders them **locked** (LockedKeys), free mode
    sources the run's label/task from the node. **22c:** `VisibleWhen` shows only the selected algorithm's
    hyperparameters; added `minLeaf` + Options-based SDCA/LBFGS/perceptron knobs (l1/maxIterations/historySize/
    iterations); new real trainers GAM, SGD-calibrated, Online-GD, One-vs-all-FastTree — each proven to train
    via GoldenPipelineScoringTests theories (binary 7, regression 6, multiclass 4). UnitTests 214, ComponentTests
    34 green; anti-drift guard holds. **Remaining 22d/e below.**
    Re-read **every** handler's actual `Execute` from source (not descriptors) and rewrote all **37** per-node
    docs under `node-properties/` with exact defaults/clamps, column-awareness, gaps, and (for `train`/
    `cross-validate`) the full **per-algorithm** trainer/hyperparameter breakdown. **Shipped (build-verified,
    tests pending — Web app was running/locked):** (a) **column pickers** — `Column` → dropdown of the
    dataset's real columns, `Columns` → multi-select checklist (loaded from `GetDatasetVersionAsync().Schema`),
    text-box fallback when unknown; (b) **dropdown-label fix** — every `MudSelect` uses `ToStringFunc` so the
    selected value shows its friendly label, not the raw key/GUID (verified vs `mudBlazor_Docs/Select.txt`).
    **Decision:** Option A (one Train node, algorithm dropdown, per-algorithm fields) + custom per-node editors
    where needed. **Planned:** 22b Train exposes target/id/task/features (competition pre-fills+locks, free mode
    editable); 22c per-algorithm hyperparameters (add `minLeaf`, AveragedPerceptron params, etc.) + new
    algorithms (LightGBM, GAM, SgdCalibrated, OneVersusAll, Ols) each backed by a real `MlModelOps` arm;
    22d transform/prepare gaps (scaler column selectors, hash `bits`, one-hot output-kind, featurize-text
    options, replace-missing Median); 22e Data/DuckDB builders (join-type + key picker, group-by aggregate
    builder, sort keys, condition builder). Anti-drift guard must stay green as new keys are added.

41. 🟢 **Designer page revision — dataset-as-node + de-duped run box (2026-07-27)**. The **Dataset node** now
    carries the training-dataset picker (`datasetId`): in free mode the user selects the data here and the
    whole pipeline's column pickers light up from its schema (defaults to the first dataset); in competition
    mode it's left empty (the host injects the data). The "Run pipeline" box lost its redundant dataset / label
    / task controls — data now comes from the Dataset node, target/task from the Train node (22b) — and is just
    a Run button + a one-off CSV upload. Canonical workflow = Dataset → Split → Train → Evaluate (data node +
    train + final). UnitTests 215, ComponentTests 35 green.

42. 🟡 **PLAN — whole-pipeline revision (2026-07-27)** — spec `23_PIPELINE_REVISION.md`. Full end-to-end
    re-read (designer → definition → endpoints → executor → context → nodes → scoring) found a **source-of-truth
    conflict**: Phase 22 put data/target/id/task onto the Dataset & Train nodes, but the **server ignores node
    Config** — label/id/task/data still enter as method params, reconciled only by the designer's
    `EffectiveLabel/EffectiveTask`. So a saved definition alone doesn't drive execution, `idColumn` on Train is
    decorative, and the new Dataset-node `datasetId` collides with the secondary-dataset scanner (double-load /
    latent throw) and isn't passed on competition submit (join/union pipelines can't be submitted). **Decision:**
    make the node graph the single source of truth (executor reads target/id/task from Train, datasetId from
    Dataset; competition still overrides at submit). Phases: 23a fix Dataset-node scanner/validate regression
    (do first); 23b node-driven target/id/task; 23c submit passes secondaries; 23d Execute/Predict feature-set
    parity; 23e client-side graph validation; 23f submit robustness (quota race, time budget, id coverage).
    Also logged pre-existing gaps: quota race, no `PredictAsync` time budget, tiny-key holdout degradation.
    **23a + 23b DONE (2026-07-27):** `IPipelineExecutor` label/id/task are now optional overrides — the
    executor resolves them from the Train node (`ResolveLabel/ResolveId/ResolveTask` read `targetColumn`/
    `idColumn`/`task`), so a saved `WorkflowDefinition` drives itself end to end; a competition still passes its
    own values to stay authoritative. `WorkflowDatasetScanner` now scans only join/union nodes (not the primary
    `dataset` node) + a `PrimaryDatasetId` helper; `ValidateNodeInputs` skips the primary dataset node's
    `datasetId` — no double-load / latent throw. Tests: `Graph_is_the_source_of_truth_when_no_overrides_are_passed`
    (PredictAsync with all-null overrides reads target/id/task from the graph) + `WorkflowDatasetScannerTests`.
    UnitTests 217, IntegrationTests 115 green. Pending: 23c submit secondaries, 23d feature parity, 23e client
    validation, 23f robustness.

## Global definition of done

```bash
dotnet restore
dotnet format --verify-no-changes
dotnet build --no-restore -warnaserror
dotnet test --no-build
```

Each phase must additionally pass its provider, security, UI, Worker, or ML-specific acceptance tests before the next phase begins. CI must gate all four global commands and per-phase acceptance tests on every push and pull request.

## Out-of-scope reminders

- Multi-tenant support, external community, public marketplace.
- On-prem deployment (documented as future option).
- Cross-region failover outside the Kuwait sovereign boundary.
- External / public-web competition leaderboards (internal live-vs-final leaderboards within a visibility scope are in scope).
- Notebooks execution (asset storage and versioning only).
- Arbitrary user-uploaded script execution in Worker.
- Any commercial-product surface (pricing, licensing, marketing site) — this is an internal KOC application.
