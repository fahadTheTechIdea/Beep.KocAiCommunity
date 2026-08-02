# Beep.KocAiCommunity

An **internal Kuwait Oil Company (KOC)** platform to **train and familiarize employees with AI and machine
learning**. Employees learn through guided tracks and compete in internal, Kaggle-style competitions on real KOC
data; management supervises adoption through org-scoped rollups and dashboards. This is an internal application —
**not a commercial product**.

Built on **.NET 10**, **ASP.NET Core**, **Blazor (MudBlazor)**, **EF Core**, **ML.NET**, and **.NET Aspire**.

> Design and staged plans live in [`plans/koc-ai-community-platform/`](plans/koc-ai-community-platform/README.md).

### 📚 Manuals

- **[User Guide](docs/USER_GUIDE.md)** — for employees: signing in, learning, building & running models in
  competing, and your profile.
- **[Developer Guide](docs/DEVELOPER_GUIDE.md)** — architecture, running locally, the node engine, the
  desktop app, testing, and how to extend the platform.
- **[Administrator Guide](docs/ADMIN_GUIDE.md)** — the admin console, RBAC / users, competition-creation
  grants, org codes, demo data, settings, and audit.
- **[Deployment Guide](docs/DEPLOYMENT.md)** — hosting, auth modes, SQL Server, publishing.
- **[Visual Help](docs/help/index.html)** — one HTML page with a screenshot tour of every screen **plus** the
  User, Administrator, and Developer guides (open in a browser).

## The loop

**Dashboard → Learn → Build in KOC Studio (desktop) → Compete → Model registry → Deploy** — with live notifications and
management supervision over all of it.

## Surfaces

| Surface | Route | What it does |
|---|---|---|
| **Home** | `/` | Role switcher (Employee → CEO), KOC "blueprint" theme, brand assets |
| **Dashboard** | `/dashboard` | Your learning + competition standings; a team overview if you lead people |
| **Learn** | `/learn` | Guided tracks with real markdown lessons → enroll → complete → progress/completion |
| **Compete** | `/compete` | Create a challenge (lifecycle + reveal day) → download data → submit predictions → **live** leaderboard |
| **Community** | `/community` | Discussions and replies |
| **Supervision** | `/supervision` | A supervisor's read-only rollup of their people's learning + competition activity |

A **notification bell** (live, per-user) sits in the app bar across every surface.

> **Building and training happen in KOC Studio, the desktop app** — datasets, the node designer, AutoML,
> run history, the local model registry and the node catalogue all live there, not on the website. See
> [`docs/STUDIO_IS_A_DESKTOP_APP.md`](docs/STUDIO_IS_A_DESKTOP_APP.md).

## Machine learning

- **Node editor** (`/workflow`): a real drag-to-connect canvas with an 11-node catalog — `dataset`,
  `select-columns`, `sample`, `one-hot`, `replace-missing`, `normalize`, `split`, `train`, `cross-validate`,
  `score`, `evaluate` — executed **node by node**, each reporting live status.
- **Per-node config + hyperparameters**: algorithm choice plus trees / leaves / learning-rate (FastTree/FastForest)
  and L2 (SDCA/LBFGS); blank fields fall back to ML.NET defaults.
- **Three task types**: binary (accuracy), multiclass (MicroAccuracy), regression (RMSE) — type-aware transforms
  and trainers throughout (SDCA, FastTree, FastForest, LBFGS, AveragedPerceptron, SdcaMaximumEntropy, NaiveBayes).
- **AutoML** (`/studio`) via `Microsoft.ML.AutoML`, time-boxed, recorded as immutable training runs.

## Competitions

- A competition owns **training data** (visible), an **evaluation feature set** (visible, no label), and a
  **hidden answer key**. Scored by trusted server-side plugins: `accuracy` (classification) or `rmse` (regression).
- **Pipeline submissions**: a participant submits their node graph; the server runs it on the *authoritative*
  data (no tampering) → predictions → score → leaderboard.
- **Lifecycle**: draft → active → concluded, with a **reveal-day final leaderboard** kept hidden until reveal time.
- **Live**: submitting pushes a `leaderboard.updated` event to the competition's SignalR group; open boards refresh
  in place. Concluding notifies every participant.

## Notifications & real-time

A per-user `Notification` feed, emitted by domain events (submission scored, competition concluded, model
promoted/deployed). Events flow through a **transactional outbox**; the dispatcher routes each to the right SignalR
group (`user:{id}` / `competition:{id}`). The bell and leaderboards update live, with graceful fallback to polling.

## Solution layout

```
src/
  Beep.KocAiCommunity.Domain            Entity types + enums (no EF, no framework)
  Beep.KocAiCommunity.Contracts         DTOs shared across hosts
  Beep.KocAiCommunity.Application        Service interfaces, role/policy constants, domain events
  Beep.KocAiCommunity.Workflow           Workflow compiler (validate + topological order)
  Beep.KocAiCommunity.ML                 ML.NET runtime: AutoML trainer + node-by-node pipeline executor
  Beep.KocAiCommunity.Infrastructure     EF Core (DbContext, configs, migrations), services, storage, seeders
  Beep.KocAiCommunity.Infrastructure.SqlServerMigrations   Provider-specific SQL Server migrations
  Beep.KocAiCommunity.Ui.Shared          MudBlazor theme + shared components (KOC blueprint)
  Beep.KocAiCommunity.Ui.Community/Studio/Admin   Feature RCLs (Ui.Studio is desktop-only)
  Beep.KocAiCommunity.Client             Framework-agnostic HTTP API client + dev identity (Web + desktop)
  Beep.KocAiCommunity.ServiceDefaults    Aspire defaults + shared security wiring
  Beep.KocAiCommunity.Web                Blazor Web App (Interactive Server) — calls the API, live via SignalR
  Beep.KocAiCommunity.Api                Minimal API (/api/v1) + SignalR hub + outbox dispatcher
  Beep.KocAiCommunity.Worker             Background worker
  Beep.KocAiCommunity.Desktop.Local      Offline in-process Studio engine for the desktop (LocalKocApiClient)
  Beep.KocAiCommunity.WinForms           WinForms desktop app hosting the Studio designer via BlazorWebView
  Beep.KocAiCommunity.AppHost            .NET Aspire orchestration
tests/                                    Unit, Integration, Component (bUnit), Architecture, EndToEnd
```

Backing all of it: Microsoft Entra authentication (config-driven), the KOC org hierarchy
(Team ⊂ Group ⊂ Directorate ⊂ Company) with position roles (Employee → TeamLeader → Manager → DCEO → CEO),
org-scoped visibility, and governed artifact storage with information-security classification.

Dependency direction is enforced by `ArchitectureTests` (Domain/Application stay free of EF, ASP.NET, MudBlazor,
and ML.NET; the Web calls the API, never the database).

## Run it

Prerequisites: **.NET SDK 10** (pinned in `global.json`).

### Standalone (no Docker)

Two terminals — the API auto-migrates a local SQLite database, seeds starter content (learning tracks + two demo
competitions), and enables a **development-only auth** (no Entra tenant needed):

```bash
# Terminal 1 — API on http://localhost:5250 (migrate + seed + dev auth)
ASPNETCORE_ENVIRONMENT=Development Seed__Enabled=true ASPNETCORE_URLS=http://localhost:5250 \
  dotnet run --project src/Beep.KocAiCommunity.Api

# Terminal 2 — Web on http://localhost:5150 (calls the API)
ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://localhost:5150 \
  dotnet run --project src/Beep.KocAiCommunity.Web
```

Open <http://localhost:5150>. The **Dashboard**, **Datasets**, **Compete**, and **Models** pages let you change the
acting-as dev user to see visibility, leaderboards, and dashboards behave per person. The dev user is seeded as a
**Manager**, so **Supervision** and the team overview show populated rollups. Two demo competitions (a
classification and a regression one) come pre-loaded with data so you can submit a pipeline immediately.

### Desktop app (offline Studio)

A **Windows desktop** version of the Studio designer that hosts the *same* Blazor components inside a
WinForms window via **BlazorWebView**. It is **offline-first**: the node palette, local CSV datasets, and
**pipeline runs** all execute **in-process** (the same ML.NET + DuckDB engine) with no web server. Only
competitions (browse, submit, leaderboard) call the API — offline they degrade gracefully.

```bash
# Runs fully offline — no API needed for the designer + local runs.
dotnet run --project src/Beep.KocAiCommunity.WinForms
```

- Drop CSV files into `%LOCALAPPDATA%\KocStudio\datasets\`; they appear in the designer's Run panel.
  Workflows are saved as JSON under `%LOCALAPPDATA%\KocStudio\workflows\`.
- **Settings** sets the API base URL (for competitions) and the dev persona; `KOC_API_BASEURL` overrides.
- Requires the **WebView2 runtime** (standard on Windows 10/11). Distribute with:
  `dotnet publish src/Beep.KocAiCommunity.WinForms -c Release -r win-x64` (bundles the ML.NET/DuckDB
  `win-x64` natives under `runtimes/`).

### Aspire (orchestrated dashboard)

`aspire run` orchestrates the API, Worker, and Web behind the Aspire dashboard. **In dev it uses the
same built-in SQLite as standalone mode — no Docker, no SQL Server container** — so it starts in
seconds. The dashboard is pinned to a fixed URL (`http://localhost:15130`) and opens automatically:

```bash
dotnet run --project src/Beep.KocAiCommunity.AppHost
```

Open the dashboard (link printed in the terminal / auto-opened), then click the `web` resource.

To exercise the **production-shaped stack** (SQL Server in a container, which pulls a ~1.7 GB image
the first time) locally, opt in — this requires Docker:

```bash
dotnet run --project src/Beep.KocAiCommunity.AppHost -- UseSqlServer=true
```

When **publishing** (`aspire publish` / `aspire deploy`), SQL Server is provisioned automatically and
the API/Worker switch to `Database:Provider=SqlServer` — no code change needed.

### Production auth

Set the `AzureAd` configuration (TenantId, ClientId, …) and the app switches from dev auth to Microsoft Entra
(OIDC for the Web, JWT bearer for the API). Set `Database:Provider=SqlServer` with `ConnectionStrings:kocdb` for
SQL Server.

**Intranet Windows SSO (no login).** For an on-prem intranet without Entra, set `WindowsAuth:Enabled=true` on the
**Web** and host it in **IIS with Windows Authentication enabled and Anonymous disabled**. The browser then hands
the site the signed-in domain/Entra account with no login page, and the Web forwards that **real user** to the API
(fail-closed — an unauthenticated request is never treated as the default persona). Roles default to `Employee`
until the KOC directory API maps them (see the identity seam below). The dev-persona "view as" switcher remains for
local dev (`WindowsAuth` off). The desktop app already uses the signed-in Windows account the same way.

> Identity/profile seam: `IEnvironmentUserProvider` + `EnvironmentUser` (email/company/department/roles) exist so a
> KOC directory-API provider can be dropped in later to enrich the signed-in user; per-user Blazor Server nav/roles
> reflecting the real user's directory roles is the remaining follow-up (validate in the IIS environment).

## Develop

```bash
dotnet build Beep.KocAiCommunity.slnx -warnaserror
dotnet test  Beep.KocAiCommunity.slnx
dotnet format Beep.KocAiCommunity.slnx --verify-no-changes
```

EF migrations are maintained for **both** providers — add each change twice (see `docs/DEPLOYMENT.md`):

```bash
# SQLite (dev)
dotnet ef migrations add <Name> \
  --project src/Beep.KocAiCommunity.Infrastructure \
  --startup-project src/Beep.KocAiCommunity.Infrastructure \
  --output-dir Persistence/Migrations

# SQL Server (prod)
dotnet ef migrations add <Name> \
  --project src/Beep.KocAiCommunity.Infrastructure.SqlServerMigrations \
  --startup-project src/Beep.KocAiCommunity.Infrastructure.SqlServerMigrations \
  --output-dir Migrations
```

CI (`.github/workflows/ci.yml`) runs restore → format check → build (warnings-as-errors) → test on every push and
pull request.
