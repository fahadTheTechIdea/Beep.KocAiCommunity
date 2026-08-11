# Deployment — KOC Studio (desktop)

Getting KOC Studio onto a colleague's machine. [`DEPLOYMENT.md`](DEPLOYMENT.md) covers the website and
the worker; this covers the desktop app, which ships and updates on its own schedule.

Read [`SECURITY_DESKTOP.md`](SECURITY_DESKTOP.md) first if this install will be given a database
connection — that decision is harder to reverse than the install itself.

## What it is

`Beep.KocAiCommunity.WinForms` — a Windows app that hosts the Studio designer in a `BlazorWebView`, so
the same Razor components run on the desktop as in the browser. Model building lives here and only
here (see [`STUDIO_IS_A_DESKTOP_APP.md`](STUDIO_IS_A_DESKTOP_APP.md)).

## Prerequisites on the machine

| | |
|---|---|
| **Windows** | The app targets `net10.0-windows`; the rest of the solution is cross-platform, this is not |
| **.NET 10 Desktop Runtime** | Or publish self-contained and carry it — see below |
| **WebView2 Runtime** | `BlazorWebView` will not start without it. Evergreen on current Windows; confirm on older SOE images |
| **Network** | Only for the leaderboard hub and the platform database, and only if configured. It runs offline otherwise |

## Two modes — decide before you package

**Offline (recommended default).** No platform database configured. The designer, AutoML, local
datasets and workflows all work; anything needing the platform says so by name rather than failing
obscurely. This is the right build for classrooms, pilots and anyone who is learning.

**Connected.** A platform connection string is supplied, and Studio reads competitions, submits
entries and records runs directly against the database. Only for people who need it, and only with the
SQL identity decided in `SECURITY_DESKTOP.md` §3.

## Build

```bash
# Framework-dependent — smaller, needs the .NET 10 Desktop Runtime on the machine
dotnet publish src/Beep.KocAiCommunity.WinForms -c Release -r win-x64 --self-contained false

# Self-contained — larger, carries the runtime, no prerequisite beyond WebView2
dotnet publish src/Beep.KocAiCommunity.WinForms -c Release -r win-x64 --self-contained true
```

Self-contained is usually the better trade for a managed SOE: one thing to approve, no runtime
dependency to chase.

## Configuration

Supply per install. Leave the database settings unset for the offline build.

| Setting | Purpose |
|---|---|
| `ConnectionStrings:kocdb` | The platform database. **Omit for offline.** Prefer `Integrated Security=true` so no secret is written to the machine |
| `Database:Provider` | `SqlServer` for a real platform |
| Workspace root | Defaults to `%LOCALAPPDATA%\KocStudio`. Override for a redirected-profile estate |

## First run

Studio creates its workspace — `datasets`, `workflows`, `temp`, `logs` — under
`%LOCALAPPDATA%\KocStudio`, writes a `firstrun.marker` so the welcome appears exactly once, and opens
the designer. Nothing is installed centrally and nothing is registered system-wide.

## Distribution

- **Per-user** install into the profile avoids admin rights and matches the per-user workspace.
- **Per-machine** needs packaging and admin rights; then the workspace is still per-user, which is what
  you want — one person's datasets are not another's.
- Ship through the **software portal** so the version is tracked and revocable, rather than a file share.
- The launch communications point people at the portal: see
  [`comms/ANNOUNCEMENT_EMAIL.md`](comms/ANNOUNCEMENT_EMAIL.md), which carries a `[software portal link]`
  placeholder to fill in.

## Upgrades

The workspace survives an upgrade — datasets, workflows and queued submissions are files, not app
state. Uninstalling does **not** remove `%LOCALAPPDATA%\KocStudio`; say so in the uninstall notes, or
people will assume their work went with it.

## Verify an install

| Check | Expected |
|---|---|
| App launches, designer renders | WebView2 present and working |
| Create a workflow, save it | Workspace writable |
| Import a small CSV, run AutoML | Training works; the child process respects its memory ceiling |
| **Offline build:** open Compete | Says the platform is not configured — by name, not a crash |
| **Connected build:** open Compete | Competitions listed, scoped to what that person may see |
| **Connected build:** submit an entry | Appears on the leaderboard; queued and drained if offline at the time |

## What the desktop deliberately cannot do

No RBAC console, no role granting, no competition administration, no answer keys. That is not an
omission to be filled in later — it is the mitigation for the workstation deciding its own user
(`SECURITY_DESKTOP.md` §1). Administration stays on the website.
