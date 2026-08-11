# Security — KOC Studio (desktop)

KOC Studio is installed on **KOC-managed Windows workstations inside the KOC environment**:
domain-joined, on the SOE image, under group policy, with endpoint protection and disk encryption
already enforced by the estate. This document assumes that and states what the application adds on
top, what it requires of the estate, and what KOC must still decide.

[`SECURITY.md`](SECURITY.md) covers the website and the platform surface. Read both.
Facts below are from the source; anything that is a KOC choice rather than an application behaviour is
marked **Decision**.

## 1. The trust shift, and why it governs everything else

Since **2026-08-02** KOC Studio **opens the platform database directly**. No API website sits between
the workstation and the data, so nothing on a server checks the desktop's work.

```
website      browser ──▶ Web ──(token the Web issued, verified server-side)──▶ data
KOC Studio   workstation ──(its own database connection)──────────────────────▶ data
```

**The process decides who the user is.** The website verifies a token it issued. On a workstation the
identity is the signed-in Windows account, taken on trust as a KOC employee (`DesktopPlatformClient`).
Anyone who can run code as that user *is* that user.

**Authorization still runs — on the workstation's word.** Studio calls the same application services
the website does, so org-scoped visibility is applied (`BrowseVisibleAsync(UserId)` and the rest).
What is absent is the server that used to vouch for `UserId`.

**The deliberate mitigation:** the desktop exposes **no administrative surface at all** — no RBAC
console, no role granting, no competition administration, no answer keys. A compromised workstation is
a compromised employee account, not a compromised platform. That absence is a control. It should not
be "filled in later".

## 2. Threat model

| Asset | Threat | Control |
|---|---|---|
| Platform database | A workstation credential reused from another machine | **Integrated auth** (§3) — the credential is the domain account, unusable elsewhere without it |
| Platform database | A user escalating their own visibility | Cannot self-grant: no admin surface on the desktop; roles are read, never written |
| Platform database | Schema damage, mass export | **Least-privilege SQL role** (§3) — no DDL, no audit-trail read |
| Datasets on disk | Laptop lost or stolen | BitLocker (estate policy) + §5 |
| Datasets on disk | Copied to removable media or personal cloud | Estate DLP — **outside this application's control** (§5) |
| The installed binaries | Tampered or spoofed build | **Code signing + managed distribution** (§6) |
| Competition integrity | Faked scores | Scoring is server-side against a hidden key; the desktop cannot score itself ([`SECURITY.md`](SECURITY.md) §6) |
| Leaver retains access | Account not revoked | Integrated auth revokes with the AD account (§10) |

## 3. Database access — the one decision that matters most

**Use Windows Integrated authentication. In a domain environment there is no good reason not to.**

```
Server=<sql-host>;Database=<db>;Integrated Security=true;Encrypt=True;TrustServerCertificate=False
```

- **No secret is written to the machine.** Nothing to extract from a config file, nothing to rotate.
- **Access follows the directory.** Disabling the AD account removes database access at the same
  moment, with no action on the endpoint.
- **Grant to an AD group** — `KOC-KocStudio-Users` or similar — not to individuals, so joiner/mover/
  leaver is a group membership change.

**The SQL role that group is given must be least-privilege.** Studio needs to read competitions,
learning content and datasets, and to write submissions, runs and its own artifacts. It must **not**
have:

- `db_owner`, `db_ddladmin`, or any DDL right — the desktop never migrates the schema
- read on the audit trail
- write to roles, grants, org units or competition definitions

**Never give a workstation the website's connection string.** The site's login is a service credential
on one controlled host. Copying it onto laptops converts a single guarded secret into as many copies as
you have installs, and removes any ability to tell one user's activity from another's.

**Decision — KOC:** the AD group, the SQL role definition, and whether workstations may reach the
database host at all, or must go through a jump/proxy path.

> **Default to no database connection.** With none configured Studio runs fully offline — designer,
> AutoML, local datasets — and anything needing the platform says so by name. That is the correct build
> for training rooms, pilots and anyone still learning, and it removes this entire section for them.

## 4. Encryption in transit

- **To SQL Server:** `Encrypt=True` with `TrustServerCertificate=False`, so the server certificate is
  validated against the KOC PKI. Do not disable validation to make a certificate warning go away.
- **To the leaderboard hub:** HTTPS/WSS to the platform website, bearer token, standard TLS validation.
- **Nothing else leaves the machine.** Training data never does (§8).

## 5. Data at rest on the workstation

The workspace is `%LOCALAPPDATA%\KocStudio` — `datasets`, `workflows`, `temp`, `logs` — created on
first run. **The application encrypts nothing.** It relies on the estate.

| Concern | Position |
|---|---|
| Full-disk encryption | **Required.** BitLocker via estate policy on any machine given a database connection or KOC data |
| Classification | The download checks that gate Confidential and Restricted datasets **do not follow a file onto a laptop**. Once exported it is a file under the user's control |
| Removable media / cloud sync | Outside this application. Ensure `%LOCALAPPDATA%` is excluded from personal cloud sync and covered by estate DLP |
| `temp` | Swept at start and after runs; a crash can leave intermediates behind until the next launch |
| Uninstall | **Does not remove the workspace.** Datasets, models and queued submissions survive. Say so in the uninstall notes, and decide whether decommissioning wipes it |

**Decision — KOC:** retention for the workspace, and whether device decommissioning must wipe
`%LOCALAPPDATA%\KocStudio` explicitly.

## 6. Software integrity and distribution

| | Position |
|---|---|
| **Code signing** | The published build **must be signed with a KOC code-signing certificate** before distribution. Unsigned, neither the estate nor the user can tell a genuine build from a substituted one. **Decision — KOC:** which certificate, and signing in the build pipeline |
| **Distribution** | Through the managed channel — SCCM / Intune / the software portal — never a file share or an emailed archive. That gives an inventory of who has which version, and the ability to withdraw one |
| **Elevation** | Per-user install into the profile needs no admin rights and matches the per-user workspace. Per-machine needs packaging and admin rights; the workspace stays per-user either way |
| **Updates** | There is **no auto-update mechanism in the application.** Versions are whatever the estate has pushed. **Decision — KOC:** the update channel and cadence, and how a build with a security fix is forced out |
| **Allow-listing** | If application control (WDAC / AppLocker) is enforced, the signed publisher must be allow-listed, including the training child process |

## 7. Endpoint protection

Studio spawns a **child process for training** and writes intermediates to `temp` at speed. On an
EDR-monitored estate that pattern draws attention.

- Test against the KOC EDR build **before** fleet rollout — a false positive that quarantines the child
  process presents to users as "training silently fails".
- Do **not** blanket-exclude `%LOCALAPPDATA%\KocStudio` from scanning. If exclusions prove necessary,
  scope them to the training executable, and record why.

**Decision — KOC:** whether any exclusion is granted, and its scope.

## 8. Training stays on the machine

AutoML runs in a **child process with a memory ceiling** (`LocalTrainingLimits`, `TrainingHost`), so a
runaway fit takes the child rather than Studio or the workstation. Server-side training no longer
exists ([`STUDIO_IS_A_DESKTOP_APP.md`](STUDIO_IS_A_DESKTOP_APP.md)).

The security consequence is favourable and worth stating to reviewers: **the data being trained on
never leaves the machine it is on.**

## 9. Logs and support

`%LOCALAPPDATA%\KocStudio\logs` holds crash and session logs. Diagnostics of a data pipeline can
contain **column names, sample values and error payloads** — that is, fragments of whatever the user
loaded.

- Treat a log bundle as carrying the classification of the data that produced it.
- **Decision — KOC:** whether users may send logs to support unreviewed, retention on the endpoint, and
  whether Windows Error Reporting is permitted to leave the estate for this application.

## 10. Joiner, mover, leaver, and lost devices

| Event | With Integrated auth | With a stored credential |
|---|---|---|
| Leaver | Disable the AD account — database access ends immediately | Rotate the credential **on every install**, or it remains valid |
| Mover | Group membership change; visibility follows their new org placement on next use | Same rotation problem if scope changes |
| Lost or stolen device | Disable the account; BitLocker protects data at rest | The credential is on the disk. Assume compromise and rotate |

That asymmetry is the strongest practical argument for §3.

**Decision — KOC:** the incident procedure for a lost machine that held a platform connection, and who
is notified.

## 11. Shared and multi-user machines

The workspace is per-user, so one person's datasets are not another's. On a shared training PC:

- Every user gets their own `%LOCALAPPDATA%\KocStudio`; data accumulates per profile.
- With Integrated auth the identity is whoever is signed in — correct behaviour, and another reason not
  to use a shared service credential.
- **Decision — KOC:** whether shared/kiosk machines get the offline build only.

## 12. Supply chain

The application carries third-party dependencies (ML.NET and the .NET stack via NuGet) and requires the
**WebView2 runtime**, which is evergreen and updated by Microsoft outside this application's release
cycle. `NuGetAuditMode` is set to audit direct dependencies at build time.

**Decision — KOC:** whether WebView2's evergreen update channel is acceptable or must be pinned, and
where dependency scanning reports go.

## 13. What the desktop deliberately cannot do

No RBAC console, no role granting, no competition administration, no answer keys, no schema changes.
This is the compensating control for §1 and should be verified as still true at each release.

## 14. Pre-rollout checklist

- [ ] **Integrated authentication** via an AD group — no credential on any endpoint
- [ ] SQL role reviewed: no DDL, no audit read, no writes to roles/grants/competition definitions
- [ ] Confirmed the website's connection string is **not** used anywhere on a workstation
- [ ] `Encrypt=True`, `TrustServerCertificate=False`, server certificate validating against KOC PKI
- [ ] Build **code-signed**; publisher allow-listed if application control is enforced
- [ ] Distributed through the managed channel with version inventory and a withdrawal path
- [ ] Update channel and cadence agreed, including forced rollout of a security fix
- [ ] BitLocker confirmed on every machine receiving a database connection
- [ ] EDR tested against the training child process; exclusions scoped and justified if any
- [ ] Log handling, retention and support-bundle rules agreed
- [ ] Lost-device and leaver procedures written and owned
- [ ] Training rooms and shared machines receive the **offline** build
- [ ] Uninstall/decommission behaviour for `%LOCALAPPDATA%\KocStudio` agreed
