# KOC Studio (WinForms Desktop) — evolution plan

The desktop Studio was built to prove that a KOC engineer can design and run an ML pipeline with no web
server. It does that. This folder plans its evolution into something an engineer can work in all day.

**Start here:** [`MASTER_TODO_TRACKER.md`](MASTER_TODO_TRACKER.md)

## Why now

The deployment decision of 2026-08-02 changed the desktop's job. The pilot runs on shared hosting that
cannot run a Windows Service, so the Worker has nowhere to live and **all model training moves to the
desktop**. A demo became the training tier.

## The documents

| # | Phase | What it covers |
|---|---|---|
| [00](00_RESEARCH_AND_DECISIONS.md) | Research and decisions | Online research and the nine decisions the rest of the plan rests on |
| [01](01_WORKSPACE_FIRSTRUN_DIAGNOSTICS.md) | Workspace, first run, diagnostics | Stop failing silently; logs, crash handling, WebView2 check, first-run |
| [02](02_DATA_IMPORT_PROFILING_PREVIEW.md) | Data | Import *(part shipped)*, encoding/delimiter detection, profiling, large files |
| [03](03_DESIGNER_UX_PARITY.md) | Designer UX | Per-node data preview, palette search, live validation, undo/redo |
| [04](04_LOCAL_AUTOML_AND_RUN_HISTORY.md) | Local AutoML + run history | **The phase the pilot depends on** |
| [05](05_LOCAL_MODEL_REGISTRY_AND_INFERENCE.md) | Models | Keep, predict, export, promote |
| [06](06_OFFLINE_FIRST_COMPETITIONS.md) | Offline competitions | Cache, outbox, sync, conflicts |
| [07](07_PACKAGING_UPDATES_WEBVIEW2.md) | Packaging | Installer, updates, WebView2, signing |
| [08](08_ACCESSIBILITY_LOCALIZATION_TESTING.md) | Accessibility, RTL, testing | Keyboard, screen readers, Arabic, coverage, performance |

## How these differ from `plans/koc-ai-community-platform/19*`

Those documents describe the **original build** — the BlazorWebView shell, the shared RCL, the local
execution engine, the competitions bridge. They are the history and remain accurate.

This folder is the **next phase of work**, written against an audit of what actually shipped rather than
against what was intended.

## Reading the phases

Each document follows the same shape: Context · Scope (in and out) · Design · Files · Acceptance
criteria · Tests · Risks.

Two conventions worth knowing:

- **Out of scope is stated explicitly.** A phase that does not say what it is refusing is a phase that
  will grow until it is abandoned.
- **Known limitations are recorded rather than omitted.** Where something is hard — keyboard connection
  on the canvas, RTL direction for a left-to-right data flow — the document says to record the limitation
  rather than claim the capability.

## Shortest useful path

If the pilot is close and time is short: **01 → 04 → 07**.

That yields a desktop that trains models, tells you when it breaks, and can be installed and updated —
the minimum for handing it to a group of engineers rather than a developer.
