# 19 — WinForms Desktop Studio (offline-first designer + online competitions)

## Context

The Studio workflow designer today is a Blazor (MudBlazor + `Z.Blazor.Diagrams`)
page in the **Web** app that talks to the REST API over HTTP for everything —
node catalog, datasets, pipeline execution, the workflow registry, and
competitions. The ask: a **WinForms desktop version** of the Studio/designer that
reuses the same codebase, works **offline** (build and run pipelines with no web
server), and can still reach the API to **compete** when the network is available.

Confirmed decisions (with the product owner):

1. **UI = BlazorWebView hybrid.** The desktop app hosts the *exact* Blazor Studio
   components inside a WinForms window via `BlazorWebView`. Blazor runs in-process
   (no web server), so the designer, the `Z.Blazor.Diagrams` canvas, and MudBlazor
   all render on the desktop and are shared verbatim with the Web app.
2. **Runtime = local-first, competitions online.** Designing and **running**
   pipelines happen on a **local in-process engine** (the existing
   `PluginNodeExecutor` + ML.NET + DuckDB, referenced directly) against local CSV
   files. Only competition features (browse, submit, leaderboard) call the API.
3. **Scope = Designer + competitions.** The workflow designer + local run + node
   catalog, plus submitting a locally-built pipeline to a competition and viewing
   leaderboards. (Datasets/models/experiments/admin screens are out of v1.)

## Offline behaviour (the core guarantee)

| Capability | Offline (no web server) | Needs API |
|---|---|---|
| Render the designer UI (BlazorWebView, in-process) | ✅ | — |
| Node palette / property inspector (local `PluginNodeRegistry`) | ✅ | — |
| Pick a local CSV dataset | ✅ | — |
| **Run** the pipeline node-by-node (local `PluginNodeExecutor`) | ✅ | — |
| Save / open a workflow (local JSON files) | ✅ | — |
| Browse competitions · submit pipeline · leaderboards | — | ✅ |

Offline, the competitions screen shows "Connect to the KOC network to compete" and
the designer's **Submit** button is disabled with a tooltip; everything else works.

## Architecture

The Blazor `WorkflowDesigner` talks only through **`IKocApiClient`**. That single
seam is what makes the reuse work: the desktop app renders the same component but
binds `IKocApiClient` to a **local implementation** that runs the Studio surface
in-process and forwards *only the competition calls* to the real HTTP client.

```
        ┌──────────────────────── shared, unchanged ────────────────────────┐
Contracts (DTOs) ── Ui.Shared (RCL) ── Ui.Studio (RCL: WorkflowDesigner, etc.)
        │                                        │
Beep.KocAiCommunity.Client (NEW)                 │  Blazor components depend on
  IKocApiClient + KocApiClient (HTTP)            │  IKocApiClient + MudBlazor only
  DevIdentity / DevIdentityHandler / Realtime    │
        └──────────────┬──────────────────┬──────┘
                       │                  │
      Web (Blazor Server)         WinForms (BlazorWebView host)  ── NEW
      binds IKocApiClient=HTTP     binds IKocApiClient = LocalKocApiClient
                                          │
                       Beep.KocAiCommunity.Desktop.Local (NEW)
                         LocalKocApiClient : IKocApiClient
                           • catalog  → PluginNodeRegistry (ML)
                           • datasets → local CSV folder
                           • run      → PluginNodeExecutor (in-process)
                           • workflows→ local JSON store
                           • competitions → inner HTTP KocApiClient
                         references ML + Workflow + Application + Client
```

### New / changed projects

- **`Beep.KocAiCommunity.Client`** (NEW, `net10.0`) — extracted from Web:
  `IKocApiClient`, `KocApiClient` (HTTP), `DevIdentity`, `DevIdentityHandler`,
  `RealtimeOptions`, and an `AddKocHttpClient(baseUrl)` DI helper. References only
  **Contracts**. Referenced by Web and by Desktop.Local. (No Blazor/MudBlazor deps —
  `KocApiClient` already only uses `System.Net.Http.Json` + Contracts.)
- **`Beep.KocAiCommunity.Ui.Studio`** (EXISTING RCL, currently a stub) — becomes the
  home of the Studio Blazor UI moved out of Web: `WorkflowDesigner.razor`,
  `Workflows.razor`, `CreateWorkflowDialog`, `RunWorkflowDialog`, `NodeVisuals`,
  `MlNode`, the diagram helpers, and the competition submit/browse component reused
  on the desktop. References Ui.Shared + Client + `Z.Blazor.Diagrams` + MudBlazor.
- **`Beep.KocAiCommunity.Desktop.Local`** (NEW, `net10.0`) — `LocalKocApiClient :
  IKocApiClient` implementing the Studio surface locally and delegating competition
  calls to an inner HTTP `KocApiClient`. References ML + Workflow + Application +
  Client + Contracts. Testable without WinForms.
- **`Beep.KocAiCommunity.WinForms`** (NEW, `net10.0-windows`, `UseWindowsForms=true`,
  `Microsoft.AspNetCore.Components.WebView.WindowsForms`) — the desktop shell: a Form
  hosting a `BlazorWebView` whose root renders the Studio UI; DI registers MudBlazor
  services and binds `IKocApiClient` → `LocalKocApiClient`. Windows-only.

### Why this shape

- Only **one seam** (`IKocApiClient`) is swapped, so the designer component is reused
  byte-for-byte — matching "same code base".
- Local execution reuses the **existing** engine libraries (ML / Workflow /
  Application) with no fork.
- The competition path reuses the **existing** `KocApiClient` (HTTP) unchanged.
- Windows-only projects are isolated (WinForms + WebView); the shared libs stay
  cross-platform so CI on Linux still builds and tests them.

## Stages (each = a commit on `master`, its own doc, DoD gates)

Every stage ends on the global DoD: `dotnet build Beep.KocAiCommunity.slnx
-warnaserror`, `dotnet format --verify-no-changes`, full `dotnet test`, then commit
and update this tracker.

1. **Shared Client library** — extract the HTTP client + identity into
   `Beep.KocAiCommunity.Client`; Web references it; behaviour-preserving.
   → `19a_SHARED_CLIENT_AND_STUDIO_RCL.md`
2. **Studio UI → Ui.Studio RCL** — move the Studio Blazor components out of Web into
   the RCL; Web renders them from the library unchanged. → `19a_…`
3. **WinForms BlazorWebView shell (thin client)** — new WinForms project hosting the
   Studio UI, first pointed at a running API to prove the hybrid host renders and
   works end-to-end. → `19b_WINFORMS_BLAZORWEBVIEW_SHELL.md`
4. **Local execution engine** — `Desktop.Local` + `LocalKocApiClient` (catalog,
   datasets, run, workflows all local); wire the shell to local so the designer
   builds + runs a pipeline **fully offline**. → `19c_LOCAL_EXECUTION_ENGINE.md`
5. **Competitions bridge** — competition browse/submit/leaderboard via the inner HTTP
   client; offline-graceful; a settings screen for API URL + identity.
   → `19d_COMPETITIONS_BRIDGE_AND_PACKAGING.md`
6. **Packaging, config, docs** — first-run config, app icon, `win-x64` self-contained
   publish, README + tracker. → `19d_…`

## Risks & mitigations

- **BlazorWebView renders MudBlazor + Z.Blazor.Diagrams correctly?** Prove it with a
  Stage 3 smoke test before building further; both are HTML/SVG/JS and run in the
  WebView2 (Chromium) surface. WebView2 runtime ships with Win10/11.
- **`IKocApiClient` is a large interface.** `LocalKocApiClient` implements only the
  Studio + competition methods; the rest throw `NotSupportedException` (the desktop
  shows only those screens). Documented; a later refactor can segment the interface.
- **Windows-only projects in a cross-platform solution.** WinForms + Desktop projects
  target `net10.0-windows`; keep them out of the Linux CI build set (solution filter),
  shared libs remain `net10.0`.
- **Native ML.NET / DuckDB in a packaged desktop app.** Verify `win-x64` runtimes
  bundle on `dotnet publish`; smoke-test a local run from the published app.
- **Auth for competitions.** v1 uses the existing dev-header identity against a
  dev/staging API; production Entra interactive sign-in is a follow-up (noted in 19d).

## End-to-end verification

- **Offline:** disconnect the network, launch the WinForms app, drag `dataset →
  split → train → evaluate`, pick a local CSV, **Run** → node-by-node results and a
  trained metric, with no API running.
- **Online:** point the app at a running API, open Competitions, pick one, **Submit**
  the locally-built pipeline → a scored submission appears on the leaderboard.
- Per-stage unit/integration tests as described in each stage doc; the shared libs
  keep the existing suite green throughout.
