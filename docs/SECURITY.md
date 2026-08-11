# Security — website and platform

How KocAiCommunity authenticates people, decides what they may see, and protects what it holds.

> **This is one of two.** KOC Studio on the desktop reads the platform database directly, so the
> workstation — not a server — decides who its user is. That is a different trust model and it has its
> own document: [`SECURITY_DESKTOP.md`](SECURITY_DESKTOP.md). Read both before signing off the platform.

This describes the platform **as it is built today**, after the API merged into the website on
2026-08-02. Where a control exists because of that merge, it says so — the change moved a network
boundary into a process boundary, and several decisions only make sense with that in mind.

## 1. Shape of the system

One web process and one background worker, both against one SQL Server database.

```
browser ──cookie──▶  Web (Blazor Server)
                     ├── pages
                     └── Platform surface, IN-PROCESS: /api/v1 + leaderboard hub
                                    ▲
KOC Studio (desktop) ──bearer───────┘  (hub only; the desktop reads the database directly)
                                    │
Worker ─────────────────────────────┴──▶ SQL Server
```

There is **no API website**. `Beep.KocAiCommunity.Platform` is a library the website hosts in its own
process, and the website reaches it over loopback rather than the network. Three consequences follow:

- The website owns the database connection. It is the thing to secure, not a second tier behind it.
- `/api/v1` has no remote caller by design, so it is closed by default (§4).
- There is one secret store, not two. The user-secrets id is `beep-kocaicommunity-web`; the old
  `beep-kocaicommunity-api` store is dead and anything kept there had to be re-set.

## 2. Who a visitor is

Where people sign in is settled once, at first run, and recorded in `Auth:SignInWith`
(`KocSetupStore`). Configuration always wins over the stored answer, so a deployment can decide it
without ever seeing the wizard.

| Source | Meaning | Where it fits |
|---|---|---|
| `KocEnvironment` | The corporate Windows account, already verified by IIS before the request arrives. Nothing to configure — IIS *is* the configuration. | KOC intranet |
| `SiteAccounts` | Accounts belonging to this site: people register and sign in with a password here. | Public or standalone deployments |
| Microsoft Entra | Set `AzureAd__TenantId` + `AzureAd__ClientId` and OIDC takes over for the browser, JWT bearer for `/api/v1`. Single-tenant: only the configured KOC tenant is accepted. | Azure |

**The first registration claims the platform.** On a `SiteAccounts` install the first account created
becomes the administrator (`LocalAccountService.IsFirstAccountAsync`). Register immediately after
deploying, before the address is shared — there is no second chance and no approval step.

Local accounts use ASP.NET Core Identity: minimum 8 characters, lockout after 10 failed attempts for
15 minutes, and one message for "no such account" and "wrong password" so the login page does not
disclose which emails exist.

The browser session is a cookie named `koc.auth` — `HttpOnly`, `SameSite=Lax`, 8-hour sliding
expiry, `SecurePolicy = SameAsRequest`. That last one means **the cookie is only marked Secure when
the request arrived over HTTPS**: on a deployment served over plain http it will travel in clear.
Terminate TLS in front of the app and leave the HTTPS redirect on.

## 3. Access tokens

Anything that is not a browser — KOC Studio on the desktop, any other caller — authenticates to
`/api/v1` with a bearer token, never the cookie.

- **HS256**, signed with `Auth:TokenSigningKey`: 256 random bits generated at first run and shared by
  every host that must agree on a token. Change it and everyone is signed out.
- **8-hour lifetime**, matching the cookie, so a session does not outlive its token.
- Claims are deliberately minimal: user id (`oid` and `NameIdentifier`), display name, KOC roles, and
  a `jti`. Everything else is looked up server-side against the database, so a stale token cannot
  carry stale authority.

The signing key is a secret. In production it belongs in Key Vault or an app setting, never in a
file in source control.

## 4. `/api/v1` is closed by default

`InternalApiGuard` refuses any `/api/v1` request that did not come from this machine, answering
**404 rather than 403** — a refusal tells someone probing that there is something to find, and "no
such path" is the truthful answer from outside anyway. The leaderboard hub is deliberately exempt:
KOC Studio connects to it from a workstation for live standings, so it is the one part of the
surface with a genuine remote caller.

Two settings govern it, and on IIS they interact in a way worth understanding:

| Setting | Default | Why you would change it |
|---|---|---|
| `Platform:InternalPort` | `5151` | The app opens a second, loopback-bound listener for `/api/v1` via `UseUrls`. **IIS in-process hosting ignores `UseUrls`**, so under IIS that listener never exists and the website would be calling a port nothing answers. Set `0` there: the surface is then mapped on the site's own listener. |
| `Platform:InternalApiOnly` | `true` | With no loopback listener the website reaches its own surface over the public address, so its own requests arrive from "outside" and the guard would 404 them. Setting `false` is the price of hosting under IIS in-process — **and it makes the surface publicly reachable.** |

> **Accepted risk on the temporary demo host.** The prototype currently shown to reviewers runs with
> `InternalApiOnly=false`. Endpoints that require authentication still require it; endpoints that do
> not are readable by anyone. That is the same exposure the platform had before the merge, when the
> API was its own website — but it is exposure, and it is a deliberate trade, not an oversight.

## 5. What a person may see

Three independent things decide it.

**Position** — where someone sits in the org tree (Team ⊂ Group ⊂ Directorate ⊂ Company):
`Employee → TeamLeader → Manager → DCEO → CEO`. Position drives supervisory rollup: each level sees
the subtree beneath it, read-only.

**Function roles**, granted on top of position: `PlatformAdmin`, `CompetitionAdmin`, `LearningAdmin`,
`Auditor`. These map to policies — `RequirePlatformAdmin`, `RequireCompetitionAdmin`,
`RequireLearningAdmin`, `RequireAuditor`, `RequireSupervisor`, `RequireEmployee` — and every admin
endpoint sits behind one. The whole `/admin` group requires `RequirePlatformAdmin`.

**Org-scoped visibility** on the content itself: competitions, datasets and projects carry a
`VisibilityScope` (Team/Group/Directorate/Company) and the org unit it is relative to, so the author
chooses their audience at creation and `IVisibilityEvaluator` enforces it on read.

Creating a competition is separately **grant-gated**: a person needs an explicit
`CompetitionCreatorGrant` capping the widest scope they may publish to, so nobody quietly addresses
the whole company.

## 6. Competition integrity

The point of a leaderboard is that the score is honest.

- The **answer key is never served.** It is stored as an artifact whose id is not exposed by any
  endpoint — the DTOs project only a boolean saying a key exists. It is opened server-side, by the
  scorer, and nowhere else.
- **Scoring is server-side and trusted.** Participants submit predictions; `IScoringPlugin`
  implementations (`accuracy`, `rmse`, `auc`) compute the score against the key. A submission cannot
  report its own result.
- A scorer must match the task (`SupportedTasks`), so a competition cannot be configured to score
  regression with accuracy and produce meaningless rankings.
- Submissions are rate-limited per competition by `SubmissionQuotaPerDay`, and the concealed final
  leaderboard is revealed at `RevealUtc`.

## 7. Refusing to start unsafely

`KocProductionPreflight` runs before anything is wired and **throws** if a Production host is
configured for dev or demo. It refuses:

- `DevAuth:Enabled=true` — it authenticates every request as a dev user
- `Auth:DemoPersonas=true` — anyone may pick who to be, without a password
- `Seed:Enabled=true` — demonstration data on a real deployment
- `Database:Provider` not `SqlServer` — production running on a local SQLite file
- no sign-in source, or no token signing key

Stopping loudly at boot beats discovering any of these from the outside. The environment itself is
resolved by `KocHostEnvironment`: an explicit `ASPNETCORE_ENVIRONMENT` wins, otherwise the presence
of `appsettings.Development.json` beside the binaries decides — and production publishes exclude that
file, so a deployment resolves to Production without anyone remembering to set a variable.

## 8. Request hardening

- **HTTPS redirect and HSTS** outside Development. Note that with no HTTPS binding the redirect
  silently does nothing — see the cookie caveat in §2.
- **Antiforgery** on form posts (`UseAntiforgery`), which is what sign-in and the setup wizard use.
- **Rate limiting**: 1000 requests/minute globally, and **20/minute on the authentication endpoints**
  specifically, rejecting with 429.
- **Artifacts live outside `wwwroot`** (`Artifacts:RootPath`, e.g. `App_Data\artifacts`), so uploaded
  and generated files are served only through code that checks who is asking, never as static files.
- Uploads are bounded by size and extension allow-list (`ArtifactUploadOptions`) and carry a
  `KocDataClassification`.

## 9. Audit

`IAuditEnvelope` writes an entry for administrative and consequential actions — role changes, grants,
category edits, demo seeding — recording actor, action, resource, and before/after JSON. The trail is
readable at `/admin` by `PlatformAdmin` or `Auditor` and is intended to be retained for seven years.

## 10. Secrets

Never commit one. `appsettings.json` holds logging and `AllowedHosts` and should stay that way.

| Environment | Where secrets live |
|---|---|
| Local dev | User Secrets (`beep-kocaicommunity-web`). Default provider is SQLite, so usually there is nothing to set. |
| On-prem IIS | **Prefer passwordless**: run the app pool as a dedicated service account or gMSA with a SQL login, and use `Integrated Security=true`. Nothing to store or rotate. |
| Azure | **Prefer Managed Identity** for SQL and Blob (`Authentication=Active Directory Default`). Any residual secret goes in Key Vault. |
| Temporary demo host | No Key Vault and no managed identity exist there. Secrets sit in a gitignored `appsettings.Production.json` deployed with the build. Applies only while a prototype is being shown; not a pattern for a KOC deployment. |

## 11. Known limitations of the public demo

The prototype is currently shown from a **third-party shared-hosting account on the public internet** —
a temporary arrangement for review, not a deployment target and not part of this project's architecture.
Accepted for a demo, unacceptable for anything real:

- **`/api/v1` is publicly reachable** (§4).
- **`Encrypt=False` on the SQL connection.** It is the hosting provider's own guidance — website and
  database share their internal network — but the traffic is unencrypted.
- **Site accounts, not Entra.** Anyone who reaches the address can register. There is no approval
  step and no tie to a KOC identity.
- **Shared tenancy.** The database and the web process sit on infrastructure shared with unrelated
  customers, outside any KOC or Kuwait data-residency boundary. Nothing confidential belongs on it.

The intended production path — Entra, Managed Identity, Key Vault, Azure Kuwait Central — is in
[`DEPLOYMENT.md`](DEPLOYMENT.md).
