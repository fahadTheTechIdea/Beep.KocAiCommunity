# Beep.KocAiCommunity — Implementation Plan

**Plan folder:** `plans/koc-ai-community-platform/`
**Status:** 🟢 BUILDING — foundation/auth/data/API/shell/competitions/learning shipped (see `MASTER_TODO_TRACKER.md` for the 2026-07-19 code audit); engagement (05b) and Worker (10) are next
**Goal:** Build a dedicated, single-tenant, **internal** platform for Kuwait Oil Company (KOC) whose primary purpose is to **train and familiarize KOC employees with AI and machine learning**. Employees learn through guided learning tracks and then test themselves in **internal, Kaggle-style competitions** scored on hidden KOC data, all on real upstream / production / facilities datasets. Management supervises adoption and skill growth through **org-scoped rollup dashboards** that follow the KOC reporting line. It is built on .NET 10, MudBlazor 9, and ML.NET 5, combining the Community collaboration surface and the Studio ML workflow/experiment surface into one application. This is an internal KOC application, **not a commercial product** — nothing is sold, licensed, or exposed outside KOC.

## Decision summary (confirmed 2026-07-17)

1. **Purpose is upskilling, not production ML ops.** The platform exists to raise AI/ML capability across the KOC workforce. Learning tracks and internal competitions are the core surfaces; the ML Studio (workflows, experiments, models) is the hands-on toolkit that supports them. Employees are the primary participants and "have the most fun"; managers supervise.
2. **Single-tenant KOC deployment.** Only the KOC Entra tenant is supported. No tenant switcher, no industry selection, no public marketplace.
3. **KOC employees only.** External community, public profiles, open competitions, and external leaderboards are out of scope.
4. **KOC org hierarchy is first-class.** KOC is modelled as Teams ⊂ Groups ⊂ Directorates ⊂ Company (KOC). People hold a position level in the reporting line: **Employee → Team Leader → Manager → DCEO → CEO** (a Team Leader leads a Team, a Manager a Group, a DCEO a Directorate, the CEO the Company). Each level above Employee sees supervisory rollup dashboards for the org subtree beneath it; it does not compete on their behalf.
5. **Org-scoped visibility on creation.** When anyone (admin or employee) creates a **competition, dataset, or project**, they choose *who can see it* — **Team, Group, Directorate, or Company (all KOC)** — defaulting to the creator's own units, with an audience preview. This replaces the old private/project/public-to-KOC visibility enum.
6. **Learning tracks are a first-class feature.** Guided, level-graded tracks (Getting started → Solve a real problem → Make it dependable) with lessons, enrollment, and progress, tied into competitions and the supervision dashboards.
7. **Oil & gas domain only.** Workflows, scenarios, templates, datasets, and node palette are upstream / midstream / downstream / HSE. Other industries are out of scope.
8. **KOC enterprise integrations.** First-class connectors for PPDM 39, OpenWells, EcoSys, SAP, AVEVA PI historian, and ADLS Gen2 — as governed sources for learning and competition datasets.
9. **ASP.NET Core 10 (LTS) + MudBlazor 9.7** Blazor Web App with Interactive Server render mode.
10. **EF Core only.** SQL Server/Azure SQL in production; SQLite in development and tests. No BeepDM for application data.
11. **Authentication:** Microsoft Entra ID workforce tenant (KOC tenant only).
12. **Authorization:** Entra app roles for position levels (`Employee`, `TeamLeader`, `Manager`, `DCEO`, `CEO`) and platform functions (`PlatformAdmin`, `CompetitionAdmin`, `LearningAdmin`, `Auditor`), plus org-subtree scoping and EF-backed resource permissions/visibility.
13. **Deployment shape:** Web + API + ML Worker orchestrated by .NET Aspire AppHost. Aspire 13.4.
14. **Workflow editor:** Z.Blazor.Diagrams 3.0.4.1 with custom MudBlazor nodes.
15. **Experiment tracking:** native EF Core tracker using ML.NET `IMonitor`. MLflow is an optional sink only.
16. **Background execution:** EF-backed durable job queue in the Worker. Stream progress through SignalR via a transactional outbox.
17. **Community-first, fun UX (added 2026-07-19).** The platform must feel like a social community, not an enterprise tool: employees earn **Barrels (bbl)** for learning/competing/collaborating, climb an O&G career ladder (Roustabout → Chief Geoscientist), collect badges drawn from the shipped O&G icon library, give kudos, keep streaks, and rally behind **Team-vs-Team leaderboards** built on the KOC org tree. Celebrations are warm and never shaming (top-10 + your-rank views only). See `05b_COMMUNITY_ENGAGEMENT_AND_GAMIFICATION.md`.

## Plan structure

| # | File | What it covers |
|---|------|----------------|
| 00 | `00_RESEARCH_AND_ARCHITECTURE_DECISIONS.md` | Research summary and rationale per decision |
| 01 | `01_SOLUTION_FOUNDATION.md` | Solution layout, Aspire, project graph, central package management |
| 02 | `02_ENTRA_ID_SECURITY_AND_RBAC.md` | Microsoft Entra, position-level app roles, KOC org hierarchy, org-subtree scoping, visibility model, audit envelope |
| 03 | `03_EF_CORE_DATA_AND_ARTIFACT_STORAGE.md` | DbContext, entities, migrations, IArtifactStore |
| 04 | `04_API_CONTRACTS_AND_REALTIME_EVENTS.md` | `/api/v1` and `/admin/api/v1` routes, SignalR, outbox |
| 05 | `05_MUDBLAZOR_SHELL_AND_SETUP.md` | Blazor shell, MudBlazor providers, navigation, KOC theming |
| 05b | `05b_COMMUNITY_ENGAGEMENT_AND_GAMIFICATION.md` | Barrels XP, O&G career ladder, badges, kudos, streaks, team leaderboards, celebrations — the community-fun layer |
| 06 | `06_COLLABORATION_AND_DISCUSSIONS.md` | Profiles, discussions, activity, mentions, moderation |
| 07 | `07_DATASETS_PROJECTS_AND_COLLABORATION.md` | Datasets, projects, collaborators, data classification |
| 07a | `07a_KOC_ENTERPRISE_CONNECTORS.md` | PPDM, OpenWells, EcoSys, SAP, AVEVA PI, ADLS Gen2 connectors |
| 08 | `08_MLNET_RUNTIME_AND_NODE_CATALOG.md` | ML.NET contracts, node registry, O&G node catalog |
| 09 | `09_WORKFLOW_DESIGNER_COMPILER_AND_VERSIONING.md` | Z.Blazor.Diagrams proof, typed WorkflowDefinition JSON, compiler |
| 10 | `10_WORKER_EXECUTION_AND_RUN_ORCHESTRATION.md` | EF durable queue, leases, retries, real-time progress |
| 11 | `11_EXPERIMENT_TRACKING_AND_EVALUATION.md` | Experiments, runs, ML.NET IMonitor, comparison |
| 12 | `12_MODEL_REGISTRY_AND_INFERENCE.md` | Model versions, lifecycle, inference endpoints |
| 13 | `13_COMPETITIONS_AND_LEADERBOARDS.md` | Internal KOC competitions, scoring, leaderboards, org-scoped visibility |
| 13a | `13a_LEARNING_TRACKS_AND_UPSKILLING.md` | Guided learning tracks, lessons, enrollment/progress, supervision rollups |
| 14 | `14_INDUSTRY_TEMPLATES_ADMIN_AND_HELP.md` | O&G templates, ML/Community/Competition admin, help system |
| 14a | `14a_PLATFORM_ADMIN_SETTINGS_AND_AUDIT.md` | Platform admin, typed settings service, audit |
| 15 | `15_TESTING_HARDENING_DEPLOYMENT_AND_MIGRATION.md` | Test matrix, CI, security, deployment, migration |
| — | `references/` | Matrices and reference catalogs that span all stages |
| — | `MASTER_TODO_TRACKER.md` | Master checklist |

## Per-stage document layout

Every stage document (`NN_*.md` and `14a_*.md`) follows this structure:

1. Goal and dependencies
2. Existing reference behavior
3. Architecture decisions for this stage
4. Project-by-project deliverables
5. Entities and migrations (where applicable)
6. API contracts (where applicable)
7. MudBlazor pages and components (where applicable)
8. Security and authorization requirements
9. Tests
10. Verification commands
11. Acceptance gate
12. Risks and deferred work

## Cross-cutting references (in `references/`)

- `FEATURE_PARITY_MATRIX.md` — maps every Python feature in Beep.AI.Community / Beep.AI.MLStudio to a .NET landing zone or a deferral.
- `TECHNOLOGY_MATRIX.md` — pinned package versions and refresh cadence.
- `SOLUTION_DEPENDENCY_MAP.md` — project dependency graph with rationale.
- `DATA_MODEL_CATALOG.md` — entity-by-entity table for the EF Core model.
- `API_ROUTE_CATALOG.md` — route-by-route table for `/api/v1` and `/admin/api/v1`.
- `AUTHORIZATION_MATRIX.md` — Entra app roles, resource permissions, policies.
- `WORKFLOW_NODE_CATALOG.md` — node-by-node list with ports, inputs, outputs, trainer support.
- `TEST_AND_ACCEPTANCE_MATRIX.md` — required test categories and per-stage gates.
- `RISKS_AND_DEFERRED_SCOPE.md` — explicit out-of-scope items and rationale.

## Key research sources

- [.NET support policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core) — .NET 10 LTS through Nov 2028.
- [ASP.NET Core Blazor](https://learn.microsoft.com/en-us/aspnet/core/blazor/?view=aspnetcore-10.0) — Interactive Server render mode.
- [MudBlazor latest release](https://github.com/MudBlazor/MudBlazor/releases/latest) — MudBlazor 9.7.0.
- [ML.NET AutoML API](https://learn.microsoft.com/en-us/dotnet/machine-learning/how-to-guides/how-to-use-the-automl-api) — ML.NET 5.0, AutoML 0.23.0.
- [Blazor.Diagrams](https://github.com/Blazor-Diagrams/Blazor.Diagrams) — Z.Blazor.Diagrams 3.0.4.1.
- [Microsoft Entra ASP.NET Core authentication](https://learn.microsoft.com/en-us/entra/identity-platform/tutorial-web-app-dotnet-prepare-app) — Microsoft.Identity.Web 4.13.2.
- [.NET Aspire](https://learn.microsoft.com/en-us/dotnet/aspire/get-started/build-your-first-aspire-app) — Aspire AppHost 13.4.6.
- [MLflow tracking](https://mlflow.org/docs/latest/ml/tracking/) — reference for optional sink adapter.

## Local SDK baseline (verified 2026-07-17)

```
.NET SDK 10.0.302
Runtime 10.0.10 (LTS)
Workloads: maui-windows, android, ios, wasm-tools
```

The Aspire workload is not installed locally; Aspire templates ship as NuGet packages so the absence of the `aspire` workload is not a blocker.
