# Beep.KocAiCommunity — Master Todo Tracker

**Plan folder:** `plans/koc-ai-community-platform/`
**Status:** 🟡 PLANNING — research complete; per-stage documents being implemented
**Goal:** Build a dedicated, single-tenant, internal platform for Kuwait Oil Company (KOC) that trains and familiarizes employees with AI/ML through guided learning tracks and internal, Kaggle-style competitions, with management supervision via org-scoped rollups. Built on .NET 10, MudBlazor 9, and ML.NET 5. Internal KOC application — not a commercial product.

---

## Phase checklist

- [ ] **Phase 00 — Research and architecture decisions** (`00_RESEARCH_AND_ARCHITECTURE_DECISIONS.md`)
  - [ ] Document research findings from `Beep.AI.Community`, `Beep.AI.MLStudio`, `Beep.AI.Server`, `Beep.ML.NET`, `Beep.AI.Shared`
  - [ ] Document BeepWeb and Beep.OilandGas.Web precedents
  - [ ] Document the Z.Blazor.Diagrams evaluation
  - [ ] Document ML.NET `IMonitor` evaluation
  - [ ] Document Microsoft Entra ID evaluation
  - [ ] Document Aspire evaluation
  - [ ] Record pinned package versions and refresh cadence
  - [ ] Record the KOC focus decisions (single tenant, KOC-only, O&G only, KOC integrations)
  - [ ] Record out-of-scope and deferred items

- [ ] **Phase 01 — Solution foundation** (`01_SOLUTION_FOUNDATION.md`)
  - [ ] Create solution and all projects
  - [ ] Configure `Directory.Build.props`, `Directory.Packages.props`, `global.json`
  - [ ] Configure nullable reference types, analyzers, deterministic builds, warnings-as-errors
  - [ ] Configure Aspire AppHost for Web, API, Worker, SQL Server
  - [ ] Add `Beep.KocAiCommunity.ServiceDefaults` with health checks, OpenTelemetry, resilience
  - [ ] Add unit, integration, component, architecture, and end-to-end test projects
  - [ ] Establish feature-folder conventions and dependency tests
  - [ ] Gate: Aspire launches all services; restore, format verification, build, and empty test suites pass

- [ ] **Phase 02 — Entra ID, security, and RBAC** (`02_ENTRA_ID_SECURITY_AND_RBAC.md`)
  - [ ] Register KOC Entra applications for Web and API
  - [ ] Configure Microsoft.Identity.Web for Web OIDC and API JWT validation
  - [ ] Define position-level app roles (Employee/TeamLeader/Manager/DCEO/CEO) and function roles (PlatformAdmin/CompetitionAdmin/LearningAdmin/Auditor) with policy mappings
  - [ ] Implement the KOC org hierarchy (`OrgUnit` tree Team/Group/Directorate/Company) with materialized paths and `OrgMembership`
  - [ ] Implement `IOrgScopeResolver` (supervisory subtree) and `IVisibilityEvaluator` (org-scoped visibility)
  - [ ] Define resource permissions for KOC business entities
  - [ ] Implement first-admin bootstrap for fresh databases
  - [ ] Implement audit envelope middleware
  - [ ] Gate: tenant, role, supervisory-scope, visibility, and resource-owner tests pass

- [ ] **Phase 03 — EF Core data and artifact storage** (`03_EF_CORE_DATA_AND_ARTIFACT_STORAGE.md`)
  - [ ] Create canonical application `DbContext`
  - [ ] Configure SQL Server and SQLite provider selection
  - [ ] Add provider-specific migrations
  - [ ] Add `IArtifactStore` with local filesystem and Azure Blob providers
  - [ ] Implement upload size limits, extension allowlists, hash and content inspection
  - [ ] Add KOC data classification metadata
  - [ ] Gate: migrations apply cleanly to fresh SQL Server and SQLite databases

- [ ] **Phase 04 — API contracts and real-time events** (`04_API_CONTRACTS_AND_REALTIME_EVENTS.md`)
  - [ ] Define `/api/v1` Minimal API endpoint surface
  - [ ] Add Problem Details, OpenAPI, pagination, filtering, sorting, ETags, idempotency keys
  - [ ] Add SignalR hubs for run progress, leaderboard, discussions, project collaboration
  - [ ] Implement transactional outbox pattern
  - [ ] Add API rate limits and upload-specific limits
  - [ ] Gate: contract tests, OpenAPI generation, authorization tests, SignalR reconnect tests pass

- [ ] **Phase 05 — MudBlazor shell and setup** (`05_MUDBLAZOR_SHELL_AND_SETUP.md`)
  - [ ] Implement KOC-branded shell with providers
  - [ ] Implement app bar, drawer, navigation, breadcrumbs, command palette, notifications
  - [ ] Implement setup diagnostics for Entra, API, database, Worker, artifact storage
  - [ ] Verify MudBlazor APIs against `mudBlazor_Docs/`
  - [ ] Gate: responsive layouts, keyboard navigation, route authorization, accessibility smoke tests pass

- [ ] **Phase 06 — Collaboration and discussions** (`06_COLLABORATION_AND_DISCUSSIONS.md`)
  - [ ] Implement profiles, skills, interests, avatars
  - [ ] Implement discussions, replies, votes, attachments, moderation
  - [ ] Implement notifications and mentions scoped to KOC employees
  - [ ] Implement activity feed scoped to projects the user belongs to
  - [ ] Gate: ownership, moderation, attachment security, concurrency tests pass

- [ ] **Phase 07 — Datasets, projects, and collaboration** (`07_DATASETS_PROJECTS_AND_COLLABORATION.md`)
  - [ ] Implement dataset metadata, versions, files, schemas, licenses, lineage
  - [ ] Implement dataset profiling and preview
  - [ ] Implement ML projects, collaborators, snapshots, activities
  - [ ] Implement dataset-to-project import with classification enforcement
  - [ ] Implement file, URL, and SQL import adapters
  - [ ] Implement org-scoped visibility (Team/Group/Directorate/Company) on datasets and projects with the shared `VisibilityScopePicker` + audience preview
  - [ ] Gate: dataset version immutability, permission isolation, classification enforcement, and visibility tests pass

- [ ] **Phase 07a — KOC enterprise connectors** (`07a_KOC_ENTERPRISE_CONNECTORS.md`)
  - [ ] Implement `IKocConnector` abstractions
  - [ ] Implement PPDM 39 connector with schema introspection
  - [ ] Implement OpenWells REST connector
  - [ ] Implement EcoSys connector
  - [ ] Implement SAP RFC connector
  - [ ] Implement AVEVA PI connector with AF support
  - [ ] Implement ADLS Gen2 connector
  - [ ] Implement connector catalog UI under `/connectors`
  - [ ] Implement connector health monitoring
  - [ ] Gate: each connector passes sandbox or mocked integration tests

- [ ] **Phase 08 — ML.NET runtime and node catalog** (`08_MLNET_RUNTIME_AND_NODE_CATALOG.md`)
  - [ ] Define `IMlRuntime`, `IMlTaskHandler`, `INodeDescriptor`, `INodeExecutor`
  - [ ] Implement ML.NET integration with MLContext lifetime management
  - [ ] Implement O&G-specific node catalog (production rate, reservoir, HSE, anomaly, predictive maintenance)
  - [ ] Implement AutoML trials for binary classification, multiclass classification, regression
  - [ ] Enforce split-before-fit, missing-value handling, fixed seeds, temporal splits
  - [ ] Gate: deterministic sample workflows train, evaluate, save, reload, predict

- [ ] **Phase 09 — Workflow designer, compiler, versioning** (`09_WORKFLOW_DESIGNER_COMPILER_AND_VERSIONING.md`)
  - [ ] Implement Z.Blazor.Diagrams proof of concept
  - [ ] Implement custom MudBlazor workflow nodes with typed ports
  - [ ] Implement palette, drag/drop, connect validation, pan/zoom, minimap, selection
  - [ ] Implement property inspector, schema mapping, validation panel, run controls
  - [ ] Implement application-owned `WorkflowDefinition` JSON with schema version
  - [ ] Implement immutable workflow versions, drafts, publishing, import/export, templates
  - [ ] Implement workflow compiler with cycle detection, topological ordering, type checks
  - [ ] Gate: 200-node workflow remains usable; round-trips without loss; rejects invalid connections

- [ ] **Phase 10 — Worker execution and orchestration** (`10_WORKER_EXECUTION_AND_RUN_ORCHESTRATION.md`)
  - [ ] Implement EF-backed durable job queue
  - [ ] Implement leases, heartbeat, retries, cancellation, timeout, crash recovery
  - [ ] Implement run-event outbox publisher to SignalR
  - [ ] Implement resource limits and graceful shutdown
  - [ ] Gate: runs survive Worker restarts; cancellation works; duplicate claims prevented

- [ ] **Phase 11 — Experiment tracking and evaluation** (`11_EXPERIMENT_TRACKING_AND_EVALUATION.md`)
  - [ ] Implement experiments, runs, trials, parameters, tags, metrics, snapshots
  - [ ] Implement ML.NET `IMonitor` adapter with nonblocking event channel
  - [ ] Implement experiment comparison, best-run selection, filters, lineage
  - [ ] Implement task-specific visualizations (confusion matrix, ROC/PR, residuals, forecast)
  - [ ] Implement optional `IExperimentSink` abstraction for MLflow REST export
  - [ ] Gate: multiple AutoML trials persist live; comparison reproducible; lineage complete

- [ ] **Phase 12 — Model registry and inference** (`12_MODEL_REGISTRY_AND_INFERENCE.md`)
  - [ ] Implement model artifact registration with semantic versions and checksums
  - [ ] Implement lifecycle states (staging, production, archived, rejected)
  - [ ] Implement Microsoft.Extensions.ML prediction pools
  - [ ] Implement protected batch and online inference endpoints
  - [ ] Implement promotion approvals, rollback, latency metrics, drift metadata
  - [ ] Gate: model moves from experiment to registry to inference and safely rolls back

- [ ] **Phase 13 — Competitions and leaderboards** (`13_COMPETITIONS_AND_LEADERBOARDS.md`)
  - [ ] Implement internal, Kaggle-style KOC competitions (no external sign-ups); any Employee or CompetitionAdmin can create
  - [ ] Implement org-scoped visibility (Team/Group/Directorate/Company) on competitions
  - [ ] Implement prediction-file and trusted ML.NET model submissions
  - [ ] Implement declarative scoring metrics and trusted scorer plugins
  - [ ] Implement live vs concealed-final leaderboard splits, submission quotas, reveal dates
  - [ ] Implement supervisor participation/standings rollups (scoped to caller subtree)
  - [ ] Implement real-time leaderboard SignalR updates
  - [ ] Gate: hidden evaluation data stays inaccessible; scoring reproducible; quotas work; visibility + supervisory scope enforced

- [ ] **Phase 13a — Learning tracks and upskilling** (`13a_LEARNING_TRACKS_AND_UPSKILLING.md`)
  - [ ] Seed the 3 starter tracks (Getting started → Solve a real problem → Make it dependable) with lessons
  - [ ] Implement enrollment and per-lesson progress; track completion
  - [ ] Implement learn ↔ compete tie-in (recommended competition/track)
  - [ ] Implement org-scoped visibility on tracks; author/publish gated to LearningAdmin
  - [ ] Implement supervisor track-progress rollups (scoped to caller subtree)
  - [ ] Gate: enroll idempotent; completion computed; visibility + supervisory scope enforced

- [ ] **Phase 14 — O&G templates, domain admin, and help** (`14_INDUSTRY_TEMPLATES_ADMIN_AND_HELP.md`)
  - [ ] Replace 18 industry folders with single O&G folder (upstream/midstream/downstream/HSE)
  - [ ] Implement admin pages for users, projects, datasets, workflows, jobs, experiments, models, competitions
  - [ ] Implement branding/theme settings
  - [ ] Implement tutorials, FAQ, API documentation, contextual node help, sample workflows
  - [ ] Implement data-retention and artifact-cleanup policies
  - [ ] Gate: a KOC employee can complete an end-to-end guided scenario without database intervention

- [ ] **Phase 14a — Platform admin, settings, audit** (`14a_PLATFORM_ADMIN_SETTINGS_AND_AUDIT.md`)
  - [ ] Implement PlatformAdmin policy and first-admin bootstrap
  - [ ] Implement typed settings service with categories and audit
  - [ ] Implement admin pages for users, roles, audit, sessions, health, maintenance, rate limits, branding, notifications, diagnostics
  - [ ] Implement transactional outbox publisher for admin audit
  - [ ] Implement KOC connector health overview and info-sec classification editor
  - [ ] Gate: 100% of admin endpoints return 403 to non-admin tokens; encryption hides secrets in audit JSON

- [ ] **Phase 15 — Testing, hardening, deployment, migration** (`15_TESTING_HARDENING_DEPLOYMENT_AND_MIGRATION.md`)
  - [ ] Unit, integration, API, EF-provider, Worker, ML-quality, component, Playwright, accessibility, architecture tests
  - [ ] Security tests: authorization bypass, uploads, path traversal, SSRF, archive bombs, model trust
  - [ ] Performance tests: workflow canvas, dataset preview, SignalR, queue throughput, inference
  - [ ] CI gates: restore, format, build warnings-as-errors, unit, component, integration, end-to-end, dependency, vulnerability scans
  - [ ] Deploy to Azure Kuwait region with Azure SQL and Azure Blob Storage
  - [ ] Use Managed Identity and Key Vault references
  - [ ] Backup, restore, migration, rollback, and disaster recovery procedures
  - [ ] Optional import of compatible metadata from Python Community and MLStudio SQLite
  - [ ] Gate: staging deployment passes smoke, security, migration, backup/restore, rollback exercises

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
