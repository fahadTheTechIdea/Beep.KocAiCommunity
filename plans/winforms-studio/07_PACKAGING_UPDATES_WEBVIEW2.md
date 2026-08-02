# 07 — Packaging, updates, and WebView2 distribution

> **Depends on:** 01 (a diagnosable app is a shippable app). **Gates:** any rollout past a handful of
> engineers. **Blocked on:** tracker questions 1–3.

## Context

There is no installer. To run KOC Studio today you clone the repository, install the .NET SDK and build
it. That is fine for the person who wrote it and impossible for everyone else — and it means every fix
is a manual copy to every machine.

Two decisions have to be made with KOC IT, not for them:

1. **How does the app get onto a workstation** — IT-managed, or self-service?
2. **Is there a code-signing certificate?** Unsigned installers trigger SmartScreen, and telling
   engineers to click through a security warning is a bad habit to teach on a security-reviewed
   platform.

## Scope

**In**

- Installer, chosen against the comparison below
- Auto-update with a rollback path
- WebView2 runtime handling
- Code signing
- A package KOC IT can deploy centrally
- Version visible in-app and in logs

**Out**

- A public download page. Internal distribution only.
- Cross-platform packaging. Windows only, per Phase 00.
- Silent forced updates. On a machine that may be mid-training, an update that restarts the app without
  asking is destructive.

## Decision: installer

From Phase 00 §6:

| | ClickOnce | MSIX | Velopack | Plain MSI |
|---|---|---|---|---|
| Per-user install, no admin | ✅ | ✅ | ✅ | ⚠️ usually admin |
| Auto-update built in | ✅ | ✅ | ✅ | ❌ build your own |
| Intune / SCCM deployable | ⚠️ awkward | ✅ designed for it | ⚠️ | ✅ |
| Code signing | ✅ | ✅ required | ✅ | ✅ |
| Setup effort | Low | Medium | Low | High |
| KOC IT familiarity | Medium | High | Low | High |
| 2026 standing | Legacy but *"remarkably strong"* for internal per-user auto-update apps | Microsoft's official successor | Modern, zero-config, rising | Mature |

**Recommendation: ClickOnce**, unless KOC IT wants central management — in which case **MSIX**.

The reasoning is not that ClickOnce is better technology. It is that this is exactly the case the
sources single out as its remaining strength: an internal .NET desktop app, per user, with auto-update.
MSIX is the better answer the moment IT says "we will push this through Intune", and that answer is
theirs to give.

**Velopack** is the developer-friendliest and the one KOC IT will have never seen. For a tool that IT
may have to support, that is a real cost against a modest convenience.

> **Decide with IT before building.** Reworking packaging after a pilot has installed the wrong thing is
> a migration for every user.

## Decision: WebView2

**Evergreen**, per **D3** — Microsoft's own recommendation, because security fixes arrive with Edge
rather than waiting on us to rebuild.

| Concern | Handling |
|---|---|
| Runtime absent | Phase 01 detects it and gives the link. The installer should also chain the bootstrapper |
| Windows 11 | Ships with it. Nothing to do |
| Windows 10 | May be absent. The bootstrapper covers it |
| Locked-down machines with no internet | The **offline installer** exists for exactly this; ask IT which they want |
| Version drift across machines | Accepted — that is the Evergreen bargain. Fixed Version means owning Chromium patching, which is not a job for an internal tool |

Enterprise WebView2 behaviour is configurable by Group Policy; worth telling KOC IT it exists.

## Design

### Versioning

Semantic, from a single source: `Directory.Build.props` sets `Version`, the build stamps it, and the app
shows it in Settings and writes it in every log header. A bug report without a version is a bug report
that starts with a question.

### Update flow

```
Launch
  └─ (async, non-blocking) check for an update
       ├─ none          → nothing said
       ├─ available     → an unobtrusive "Update available" in the app bar
       └─ user accepts  → download, then apply on next launch
```

Non-negotiable: **never restart the app to update while work is unsaved or a training run is in
flight.** Offer, defer, and apply on the next natural restart. An update that kills a 20-minute training
run will be the last update anyone accepts.

### Rollback

Keep the previous version. If the new one fails to start twice in a row, launch the old one and say so.
ClickOnce has this built in; MSIX has it; a hand-rolled updater would have to implement it, which is
another argument against building one.

### Signing

Sign the installer and the executable with a KOC certificate. Without it, SmartScreen warns on every
install, and an internal tool that teaches people to dismiss security warnings is doing harm beyond
itself.

If no certificate is available, that is a finding to escalate rather than a step to skip — and it should
go in the security review's gap list alongside the others.

### The build

```
build/
  publish-desktop.ps1     ← publish, package, sign, stage
  version.props           ← single source of the version
```

Publish self-contained (`win-x64`, `PublishSingleFile`) so the .NET runtime is not a prerequisite on
every workstation. It costs ~70 MB per install and removes a whole class of "it will not start" support.

## Files

| File | Change |
|---|---|
| `Directory.Build.props` | Single-source `Version` |
| `build/publish-desktop.ps1` | New — publish, package, sign |
| `WinForms/Beep.KocAiCommunity.WinForms.csproj` | Publish profile, self-contained, single-file |
| `WinForms/Services/UpdateService.cs` | New — check, notify, defer, apply |
| `WinForms/Components/Settings.razor` | Version, update channel, check-now |
| `docs/DESKTOP_INSTALL.md` | New — for KOC IT: prerequisites, deployment, rollback |

## Acceptance criteria

- [ ] A clean Windows 10 and Windows 11 VM installs and runs it with no developer tooling
- [ ] No admin rights required *(if ClickOnce or Velopack)*
- [ ] Installer and executable are signed; no SmartScreen warning
- [ ] The version is visible in Settings and in every log header
- [ ] An available update is offered, not forced
- [ ] Declining leaves the app fully working
- [ ] An update never interrupts a training run
- [ ] A failed update rolls back and says so
- [ ] WebView2 absent → the installer resolves it, or the app explains
- [ ] KOC IT has a document telling them how to deploy and roll back

## Tests

Mostly manual and environmental — packaging is not unit-testable in any meaningful way.

| Check | How |
|---|---|
| Clean-machine install | VM, both Windows versions |
| Update path | Install n, publish n+1, verify the offer and the apply |
| Rollback | Publish a deliberately broken n+1 |
| No-admin install | Standard user account |
| Signature | `signtool verify` |
| Offline install | VM with no internet, offline WebView2 bootstrapper |

Automate what can be: `publish-desktop.ps1` should fail the build if the output is unsigned or the
version is `0.0.0`.

## Risks

| Risk | Mitigation |
|---|---|
| **No code-signing certificate** | Escalate early. It has a lead time and it blocks a clean rollout |
| Wrong installer chosen, then migrated | Decide with IT before building anything |
| Self-contained size objection | ~70 MB against a whole class of support calls. Framework-dependent is the fallback if IT already manages .NET |
| Update server unreachable from the KOC network | Host it inside KOC — a file share is sufficient for ClickOnce |
| An update lands mid-training | Deferred by design; verified explicitly above |
