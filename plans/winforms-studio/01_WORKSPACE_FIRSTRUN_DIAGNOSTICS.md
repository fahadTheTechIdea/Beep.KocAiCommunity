# 01 — Workspace, first run, and diagnostics

> **Depends on:** nothing. **Blocks:** everything — a phase built on an app that fails silently is a
> phase debugged by guesswork.

## Context

Three things are true of the desktop app today and none of them are acceptable in something an engineer
relies on:

1. **An unhandled exception closes the window.** No message, no log, nothing to send to whoever supports
   it. `Program.cs` has no handler for `Application.ThreadException` or
   `AppDomain.UnhandledException`.
2. **There are no logs.** `ILogger` is registered but writes nowhere durable, so "it crashed yesterday"
   is unanswerable.
3. **A first launch shows an empty app.** No datasets, no workflows, and — until Phase 02 shipped its
   import button — no way to tell whether that was correct or broken.

There is also an environmental failure nobody has handled: **WebView2 may be absent**. Windows 11 ships
it; Windows 10 machines may not have it. Without it, `BlazorWebView` fails and the user sees a blank
window with no explanation.

## Scope

**In**

- Global exception handling with a diagnostic dialog the user can act on
- Rolling file log in the workspace, with retention
- Workspace integrity check and repair at launch
- WebView2 presence check with an actionable message
- First-run experience: a sample dataset and a guided first pipeline
- "Open logs" and "Open workspace" from Settings

**Out**

- Telemetry to a server. This is an internal tool on possibly-disconnected machines; local logs the user
  can send are the right level. Revisit only if support volume demands it.
- Crash reporting to a service, for the same reason.

## Design

### Startup sequence

```
Program.Main
  ├─ 1. Install exception handlers          ← before anything can throw
  ├─ 2. Load AppSettings (tolerate corrupt) 
  ├─ 3. Apply culture (already implemented)
  ├─ 4. Check WebView2 runtime              ← fail with guidance, not a blank window
  ├─ 5. Ensure + verify workspace           ← repair what is repairable
  ├─ 6. Build the service provider
  ├─ 7. Resolve the signed-in Windows user
  └─ 8. Show MainForm
```

Steps 4 and 5 are new. Step 1 must be first: today the handlers do not exist, and anything thrown during
composition takes the process down without a word.

### Exception handling

Two sinks, because WinForms splits them:

- `Application.ThreadException` — exceptions on the UI thread
- `AppDomain.CurrentDomain.UnhandledException` — everything else

Both write the full exception to the log and show one dialog: what happened, where the log is, and a
**Copy details** button. The dialog must not itself be able to throw — if logging fails, fall back to
`MessageBox` with the raw text.

> **Blazor's own errors do not reach either handler.** An exception inside a component is caught by the
> renderer, which by default kills the circuit. In a `BlazorWebView` this shows as a dead UI. Wrap the
> root component in an `ErrorBoundary` that logs and offers a **Reload** so a single bad page does not
> require restarting the app.

### Logging

- **Where:** `%LOCALAPPDATA%\KocStudio\logs\studio-{yyyyMMdd}.log`
- **Retention:** 14 files, deleted oldest-first at launch
- **Level:** Information by default; Debug switchable in Settings for a support session
- **Never logged:** the API token, the dev-persona override, or the contents of any dataset. File
  *names* are fine; rows are not — an engineer's CSV may be Restricted.

Add a `Microsoft.Extensions.Logging` file provider. A small hand-rolled one is sufficient and avoids a
dependency; if one is added, prefer Serilog's rolling file sink for the retention behaviour.

### Workspace integrity

`LocalWorkspace.EnsureCreated()` creates folders. It does not check whether what is there is sane. Add
`Verify()` returning a report:

| Check | On failure |
|---|---|
| `datasets/`, `workflows/`, `temp/` exist and are writable | Recreate; if not writable, fail with the path and the reason |
| `.index.json` parses | Back up to `.index.json.corrupt-{timestamp}`, rebuild by scanning `*.csv` |
| Indexed files still exist | Drop the orphan entries, log each one |
| Workflow JSON files parse | List the unreadable ones in the report; do not delete |
| `temp/` orphans older than 2 h | Sweep — `PipelineTemp.SweepOrphans` already does this; move the call here |

The report surfaces in Settings, not as a dialog. A workspace that repaired itself quietly is fine; one
that could not is what the user needs to see.

### WebView2 check

`CoreWebView2Environment.GetAvailableBrowserVersionString()` returns null or throws when the runtime is
missing. On failure, show a dialog with the download link and exit cleanly — a blank window with no
explanation is the current behaviour and it is indistinguishable from a hang.

Per **D3**, we ship Evergreen and do not bundle the runtime.

### First run

Detected by the absence of `firstrun.marker` in the workspace. On first launch:

1. Copy a small sample CSV into `datasets/` — the ESP sensor sample already used by the seeded demo
   competition, so the desktop and the platform teach the same example.
2. Land on a short welcome that names the three things the app does and links to the designer.
3. Write the marker so this never repeats.

The sample must be **obviously sample data** — named `sample-esp-readings.csv` — so nobody mistakes it
for KOC data.

## Files

| File | Change |
|---|---|
| `WinForms/Program.cs` | Exception handlers first; WebView2 check; workspace verify; first-run seed |
| `WinForms/Diagnostics/CrashDialog.cs` | New — the failure dialog with Copy details |
| `WinForms/Diagnostics/FileLoggerProvider.cs` | New — rolling file log with retention |
| `WinForms/Components/Shell.razor` | Wrap the router in `ErrorBoundary` |
| `WinForms/Components/Settings.razor` | Open logs · Open workspace · integrity report · Debug logging toggle |
| `WinForms/Components/Welcome.razor` | New — first-run page |
| `Desktop.Local/LocalWorkspace.cs` | `Verify()` returning a report |
| `Desktop.Local/LocalDatasetStore.cs` | Rebuild index from disk when it is unreadable |
| `WinForms/Assets/sample-esp-readings.csv` | New — the first-run sample |

## Acceptance criteria

- [ ] Killing the app mid-run leaves a log file naming the exception
- [ ] A thrown exception shows the dialog, not a vanished window
- [ ] An exception inside a Blazor component shows the error boundary with a working Reload
- [ ] Renaming `.index.json` to garbage and launching rebuilds it, and says so in the report
- [ ] Making the workspace read-only produces a clear message naming the path
- [ ] On a machine without WebView2, the app explains and links rather than showing a blank window
- [ ] First launch lands on the welcome page with the sample dataset present
- [ ] Second launch does not
- [ ] No dataset row content appears in any log file

## Tests

| Test | Level |
|---|---|
| `Verify()` recreates missing folders | Unit |
| `Verify()` reports an unwritable workspace rather than throwing | Unit |
| A corrupt index is backed up and rebuilt from the files present | Unit |
| Index entries pointing at deleted files are dropped | Unit |
| Log retention deletes only beyond the keep count | Unit |
| The logger never writes a configured token | Unit |
| First-run seeding is idempotent — twice leaves one copy | Unit |

The dialogs and the WebView2 check are verified by hand; a WinForms window cannot be asserted from a
test host, and this plan does not pretend it can.

## Risks

| Risk | Mitigation |
|---|---|
| The crash dialog throws while reporting a crash | It uses no DI and no logging; falls back to `MessageBox` |
| Log files grow without bound on a long session | Size cap per file as well as the file count |
| First-run sample mistaken for real KOC data | Named `sample-…`, and the welcome page says so |
| Workspace repair deletes something a user wanted | Repair never deletes data files. Only the index is rebuilt; unreadable workflows are reported, not removed |
