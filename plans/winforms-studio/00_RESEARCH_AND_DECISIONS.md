# 00 — Research and decisions

## Context

The desktop Studio's job changed on 2026-08-02. The pilot will run on MonsterASP shared hosting, which
cannot run a Windows Service — so the Worker that trained models server-side has nowhere to live, and
**training moves to the desktop**. A demo has become the training tier, and it needs to hold up.

This document records what the research said and what was decided, so later phases can cite a decision
rather than re-argue it.

---

## Research findings

### 1 · Blazor Hybrid in WinForms

The hosting model is `BlazorWebView` from `Microsoft.AspNetCore.Components.WebView.WindowsForms` — **not**
the raw `WebView2` control. The project must use the `Microsoft.NET.Sdk.Razor` SDK with a framework
reference to ASP.NET Core. Our app already does both.

Performance guidance that applies to us:

- **`ShouldRender` and `@key`** are the two levers that matter for list- and canvas-heavy UIs. The
  designer canvas re-renders more than it needs to.
- **Minimise JS interop.** Each call crosses the managed/WebView boundary. `Z.Blazor.Diagrams` already
  does a lot of it; we should not add more casually.

> Sources: [Telerik — Blazor components in WinForms](https://www.telerik.com/blogs/blazor-basics-using-blazor-components-winforms-blazor-hybrid) ·
> [Modernising WinForms with Blazor Hybrid and MudBlazor](https://jordansrowles.medium.com/modernising-winforms-with-blazor-hybrid-and-mudblazor-e3b1bb4afd79) ·
> [Blazor development best practices](https://dev.to/mescius/blazor-development-best-practices-for-2024-356c)

### 2 · WebView2 runtime distribution

Two modes, and the choice is a security trade-off rather than a preference.

| Mode | What it means | Microsoft's guidance |
|---|---|---|
| **Evergreen** | Runtime is not shipped with the app; installed once via bootstrapper, then auto-updates | **Recommended.** Minimises exposure to known vulnerabilities; security fixes arrive with Edge releases |
| **Fixed Version** | A specific runtime ships inside the app package | Only for apps with strict compatibility requirements. You own the patching |

Enterprises can configure WebView2 behaviour by **Group Policy**, which matters for a KOC rollout.

> Sources: [Evergreen vs fixed version](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/evergreen-vs-fixed-version) ·
> [Enterprise management of WebView2 runtimes](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/enterprise) ·
> [Distributing your app and the runtime](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/distribution)

### 3 · Visual pipeline designers — what the field does

Azure ML Designer is the closest analogue to our canvas, and two of its behaviours are things we lack:

- **Data preview on any node.** Right-click a component → *Preview Data* to see the intermediate table.
  This is how a user validates a pipeline step by step instead of guessing why the final metric is odd.
  We currently show only a log line per node.
- **Searchable component palette.** With enough nodes, browsing stops scaling. We have 38 node kinds
  across 9 categories and no search.

Also observed: per-component version pinning, and a canvas built on drag-and-drop with explicit
connection semantics — both of which we already have.

> Sources: [What is Designer (Azure ML)](https://learn.microsoft.com/en-gb/azure/machine-learning/concept-designer?view=azureml-api-1) ·
> [Create and run component-based ML pipelines (UI)](https://learn.microsoft.com/en-us/azure/machine-learning/how-to-create-component-pipelines-ui?view=azureml-api-2)

### 4 · Offline-first architecture

The consensus shape is five parts: **local store · durable outbox · sync worker · idempotent API ·
explicit conflict strategy**. Two findings bear directly on Phase 06:

- **Without version metadata, conflict detection is unreliable.** Any submission we queue offline must
  carry enough context to decide, on replay, whether it is still valid.
- **Test with simulated offline changes**, and use property-based tests for convergence — a sync layer
  that is only tested online is not tested.

> Sources: [Offline sync & conflict-resolution patterns](https://www.sachith.co.uk/offline-sync-conflict-resolution-patterns-architecture-trade%E2%80%91offs-practical-guide-feb-19-2026/) ·
> [Offline-first: outbox, idempotency & conflict resolution](https://www.educba.com/offline-first/) ·
> [Build an offline-first app (Android architecture guide)](https://developer.android.com/topic/architecture/data-layer/offline-first)

### 5 · ML.NET AutoML on a workstation

The most consequential finding in this document:

- **AutoML searches progressively larger models as time passes**, and users hit out-of-memory errors
  because of it. There is a standing feature request to cap memory, and
  `MaximumMemoryUsageInMegaByte` exists to do so.
- **Memory is not fully reclaimed between trials** in reported cases — usage grows across a run.
- **`RunAsync(CancellationToken)` does not return immediately.** It calls `MLContext.CancelExecution`
  and waits for running trials to finish or abort. A cancel button that pretends otherwise will look
  broken.
- Relevant knobs: `MaxExperimentTimeInSeconds`, `MaximumMemoryUsageInMegaByte`, `CacheDirectoryName`.

> Sources: [`AutoMLExperiment.RunAsync`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.ml.automl.automlexperiment.runasync?view=ml-dotnet-preview) ·
> [Add maximum memory usage limit to AutoML experiment](https://github.com/dotnet/machinelearning/issues/6293) ·
> [mlnet possible memory leak](https://github.com/dotnet/machinelearning-modelbuilder/issues/2497)

### 6 · Windows deployment in 2026

| Option | Standing | Fit for us |
|---|---|---|
| **ClickOnce** | Legacy, but *"still remarkably strong if you want to distribute an internal .NET desktop app per user, easily, with auto-update"* | Strong fit — this is precisely an internal, per-user, auto-updating app |
| **MSIX** | Microsoft's official ClickOnce replacement; containerised, enterprise management integration | Strong if KOC IT wants package identity and Intune deployment |
| **Velopack** | Introduced 2024, zero-configuration, rapidly adopted by 2026 | Simplest developer experience; least KOC IT familiarity |
| **Plain MSI** | Fine, but you build your own updater | Most work |

> Sources: [MSI vs MSIX vs ClickOnce](https://comcomponent.com/en/blog/2026/03/20/000-windows-app-deployment-msi-msix-clickonce-xcopy-custom-updater/) ·
> [Alternatives to ClickOnce (2026)](https://copyprogramming.com/howto/alternative-to-clickonce) ·
> [Choosing a deployment strategy](https://zenn.dev/jutaro0428/articles/b6aca15db09c34?locale=en)

---

## Decisions

| # | Decision | Rationale |
|---|---|---|
| **D1** | **The desktop is the training tier for the pilot.** Server-side training is not assumed. | The pilot host runs no Worker. Designing around this rather than apologising for it. |
| **D2** | **Local-first, network-optional — no feature regresses when offline** except competitions, which queue. | The existing guarantee, extended. An engineer on a rig site should lose nothing but submission. |
| **D3** | **WebView2 Evergreen**, with a launch-time presence check and an actionable message when absent. | Microsoft's security recommendation. Fixed Version means owning Chromium patching, which KOC should not take on for an internal tool. |
| **D4** | **Cap AutoML memory and time explicitly; never rely on defaults.** | AutoML grows its models until stopped, and memory is not reliably reclaimed between trials. On a workstation shared with Outlook and Teams, an uncapped run is a support ticket. |
| **D5** | **Cancellation must be honest.** The UI shows "stopping…" until the trial actually ends. | `RunAsync` waits for in-flight trials. A button that appears to do nothing is worse than one that says what it is waiting for. |
| **D6** | **Installer: ClickOnce first, MSIX if KOC IT requires it.** Decide in Phase 07 against the answers to tracker questions 1–2. | ClickOnce fits "internal, per-user, auto-update" exactly and is the least work. MSIX is the right answer only if IT wants to manage it centrally. |
| **D7** | **Run history and models are files in the workspace, not a database.** | The workspace is already the unit of backup and the thing a user can copy to another machine. Adding SQLite would create a second thing to migrate and corrupt. |
| **D8** | **The offline submission queue carries a snapshot of what it was submitted against.** | Without version metadata, replay cannot tell whether the competition has since closed or changed. |
| **D9** | **Reuse the Web's components wherever they exist.** | The designer and property panel are already shared verbatim. Every divergence doubles the maintenance and halves the testing. |

## Explicitly out of scope

- **A local copy of the community, learning tracks or leaderboards.** These are social features whose
  value is being online. Caching them would be a lot of sync machinery for something nobody needs on a
  plane.
- **Multi-user workspaces.** One workspace per Windows user.
- **Cross-platform (macOS/Linux).** WinForms plus WebView2. If this is ever wanted, it is a .NET MAUI
  rewrite of the shell, not an adjustment.
- **GPU training.** ML.NET CPU only, matching the server.
