# KOC Studio (WinForms Desktop) — Master Todo Tracker

**Plan folder:** `plans/winforms-studio/`
**Status:** 🟢 BUILDING — Phases 01, 04 and 05 code complete (2026-08-02), Phase 02 partly shipped; 03, 06–08 design-only
**Baseline audited:** 2026-08-02 against `src/Beep.KocAiCommunity.WinForms`, `src/Beep.KocAiCommunity.Desktop.Local`, `src/Beep.KocAiCommunity.Ui.Studio` at commit `e3ae815`
**Related plans:** `plans/koc-ai-community-platform/19_WINFORMS_DESKTOP_STUDIO.md` and `19a–19d` — those describe the *original* desktop build. This folder is its evolution into a product an engineer can live in.

---

## Why this plan exists

The desktop Studio was built to prove one thing: that a KOC engineer can design and run an ML pipeline
with no web server. It does that. But the deployment decision taken on 2026-08-02 changed its job —
the shared hosting that will run the pilot **cannot run the Worker**, so *all* model training now happens
on the desktop. A proof of concept has become the training tier.

That raises the bar. This plan closes the gap between "it demonstrates the idea" and "an engineer can
do a day's work in it without hitting a wall".

### What the audit found

| Area | State on 2026-08-02 |
|---|---|
| Designer canvas, node palette, property panel | ✅ Works, shared verbatim with the Web |
| Local pipeline execution (`PluginNodeExecutor`) | ✅ Works, no network |
| Workflow save / open / publish (local JSON) | ✅ Works |
| Competitions browse + submit | ✅ Works when online |
| **Dataset import** | ✅ Shipped 2026-08-02 — was the blocking gap |
| **AutoML (CSV → best model, no graph)** | ✅ Shipped 2026-08-02 — trains in a killable child process |
| **Run history** | ✅ Shipped 2026-08-02 — files in the workspace, with the dataset hash |
| **Model registry / inference** | ✅ Shipped 2026-08-02 — keep, predict, export/import; promotion blocked on the API |
| **Offline competition queue** | ❌ Submitting offline just fails |
| **Packaging / updates** | ❌ No installer, no update path |
| **Crash handling / logs** | ❌ An unhandled exception closes the window silently |
| **Accessibility** | ❌ Never assessed |

---

## Phase checklist

- [x] **Phase 00 — Research and decisions** (`00_RESEARCH_AND_DECISIONS.md`) — ✅ DONE
  - [x] Online research: Blazor Hybrid, WebView2 distribution, visual-designer UX precedent, offline-first sync, ML.NET AutoML limits, Windows deployment options
  - [x] Decisions pinned with rationale and sources

- [x] **Phase 01 — Workspace, first run, and diagnostics** (`01_WORKSPACE_FIRSTRUN_DIAGNOSTICS.md`) — ✅ CODE COMPLETE
  - [x] Workspace `Verify()` — recreates folders, reports unreadable workflows without deleting them, sweeps stale scratch, blocks on an unwritable root
  - [x] Global exception handling — `ThreadException`, `UnhandledException`, `UnobservedTaskException`; crash file + dialog with Copy details
  - [x] Blazor `ErrorBoundary` with Recover, so one bad page does not cost the session
  - [x] Rolling file log (14 files, 8 MB cap) + Open logs / Open workspace / detailed-logging toggle in Settings
  - [x] Corrupt dataset index preserved and rebuilt rather than silently discarded
  - [x] First-run sample dataset (`sample-esp-readings.csv`, generated, fixed seed)
  - [x] WebView2 presence check with a download link instead of a blank window
  - [x] 8 unit tests; full suite 529 green
  - [ ] **Hand-verification outstanding** — the crash dialog, the WebView2 message and the first-run
        experience have not been exercised in a running window. See "Definition of done" item 3

- [ ] **Phase 02 — Data: import, profiling, preview** (`02_DATA_IMPORT_PROFILING_PREVIEW.md`) — 🟡 PART SHIPPED
  - [x] Import CSV, preview header + rows, delete, open folder *(shipped 2026-08-02)*
  - [x] Name collision handling, path-traversal guard, stable ids across restart
  - [ ] Encoding and delimiter detection (semicolon/tab, UTF-8 BOM, ANSI)
  - [ ] Column profile: type, nulls, distinct, min/max/mean — reusing `CsvProfiler`
  - [ ] Large-file handling: streamed import, row-count estimate, no full read into memory
  - [ ] Dataset rename; a "recently used" ordering

- [ ] **Phase 03 — Designer UX parity with the field's best** (`03_DESIGNER_UX_PARITY.md`)
  - [ ] Per-node data preview — inspect the table flowing out of any node
  - [ ] Node palette search and keyboard-driven add
  - [ ] Validation surfaced on the canvas before a run, not only in the log
  - [ ] Undo/redo on the canvas
  - [ ] Run pane: per-node timing and row counts, kept after navigation

- [ ] **Phase 04 — Local AutoML and run history** (`04_LOCAL_AUTOML_AND_RUN_HISTORY.md`) — 🟡 CODE COMPLETE
  - [x] Register `IMlTrainer` in desktop DI; an AutoML page (CSV → best model)
  - [x] Hard memory ceiling and time budget — AutoML grows its models until stopped
  - [x] Real cancellation — **training moved to a child process**; in-process cancellation was measured
        and does not work in this ML.NET version. See the phase doc for what was measured and what it cost
  - [x] Live trial progress, streamed from the child
  - [x] Persisted local run history with metrics, limits used, and the dataset content hash
  - [x] Training limits shown and editable in Settings; Settings.razor fully localized while there
  - [x] Run comparison — tick two or more runs; rows that agree are dimmed so the differences stand out
  - [ ] **A stopped run keeps no model.** The child is killed, so it never serializes one. The UI says
        so up front; recovering it means saving the best model after each improvement
  - [ ] **Hand-verification outstanding** — a real AutoML run, Stop, and the ceiling have not been
        exercised in a running window. Same bar as Phase 01

- [ ] **Phase 05 — Local model registry and inference** (`05_LOCAL_MODEL_REGISTRY_AND_INFERENCE.md`) — 🟡 CODE COMPLETE
  - [x] Keep a run's model into a named registry entry, with metrics and lineage; versions never reused
  - [x] Predictions — one row typed in, or a CSV scored into a new file beside it
  - [x] A file missing a feature names the column, before anything is scored
  - [x] Export / import a `.kocmodel` bundle; a newer-runtime bundle is refused by name
  - [ ] **Promotion to the platform is blocked on the API** — `RegisterModelRequest` takes a
        server-side run id, and no endpoint accepts model bytes. Not buildable from the desktop; the
        page says so and points at export. See the phase doc
  - [ ] **Hand-verification outstanding** — keeping, predicting, exporting and importing have not been
        exercised in a running window

- [ ] **Phase 06 — Offline-first competitions** (`06_OFFLINE_FIRST_COMPETITIONS.md`)
  - [ ] Durable outbox for submissions made while offline
  - [ ] Background sync when the network returns; idempotent replay
  - [ ] Competition data cached for offline browsing
  - [ ] Honest connection state in the UI — no silent failures
  - [ ] Conflict rules where a submission's competition has since closed

- [ ] **Phase 07 — Packaging, updates, WebView2** (`07_PACKAGING_UPDATES_WEBVIEW2.md`)
  - [ ] Installer decision and implementation (see Phase 00 for the comparison)
  - [ ] Auto-update channel with a rollback path
  - [ ] WebView2 runtime distribution decision — Evergreen vs Fixed
  - [ ] Code signing with a KOC certificate
  - [ ] Intune/SCCM-deployable package for KOC IT

- [ ] **Phase 08 — Accessibility, localization, testing** (`08_ACCESSIBILITY_LOCALIZATION_TESTING.md`)
  - [ ] Keyboard reachability across the whole app, including the canvas
  - [ ] Screen-reader labelling; contrast audit in both themes
  - [ ] Arabic RTL verified on the desktop specifically
  - [ ] UI test coverage for the desktop shell
  - [ ] Performance budget and a measured cold-start figure

---

## Sequencing

Phases 01 and 02 are foundations — everything else is more pleasant to build once the app logs its
crashes and can profile a file. Phase 04 is the one that matters most to the pilot, because it is the
capability the server lost. Phase 07 gates any rollout beyond a handful of engineers: without an
installer and an update channel, every fix is a manual copy.

```
01 Workspace ──┬── 02 Data ──┬── 03 Designer UX
               │             └── 04 AutoML + runs ── 05 Models
               └── 06 Offline competitions
                                   │
                   07 Packaging ───┴── 08 Accessibility + testing
```

**Suggested order if time is short:** 01 → 04 → 07. That gives a desktop that trains models, tells you
when it breaks, and can be installed and updated — the minimum for handing it to a pilot group.

---

## Definition of done, per phase

A phase is DONE when all of the following hold. This mirrors the platform tracker's bar.

1. The solution builds warnings-as-errors clean.
2. New logic has tests; the full suite is green.
3. The behaviour has been exercised **in the running desktop app**, not only in tests — a WinForms
   window cannot be verified from a test host, and this plan does not pretend otherwise.
4. Anything user-visible is localized and present in `Strings.ar.resx`, held by
   `LocalizationCoverageTests`.
5. The phase document's acceptance criteria are ticked, with gaps recorded rather than quietly dropped.

---

## Open questions for the product owner

| # | Question | Blocks |
|---|---|---|
| 1 | Who installs this — IT via Intune, or engineers self-serving from a share? | Phase 07 |
| 2 | Is a KOC code-signing certificate available? Unsigned installers trigger SmartScreen. | Phase 07 |
| 3 | Is WebView2 already deployed on KOC workstations? Windows 11 ships it; Windows 10 may not. | Phase 01, 07 |
| 4 | How much of a training workload is expected per engineer? Sets the memory budget in Phase 04. | Phase 04 |
| 5 | Should the desktop work fully offline for weeks, or is it "occasionally disconnected"? | Phase 06 |
