# Publishing to SmarterASP.NET (kocaitraining.premiumasp.net)

The website — `Beep.KocAiCommunity.Web`, which carries the platform surface in-process since 2026-08-02 —
deployed to shared Windows hosting via Web Deploy. This is a demonstration/preview host, not the Azure
Kuwait Central target described in [`docs/DEPLOYMENT.md`](../../docs/DEPLOYMENT.md).

## Once, before the first publish

1. **Fill in the settings.** Copy `appsettings.Production.template.json` to `appsettings.Production.json`
   and replace `REPLACE_WITH_DATABASE_PASSWORD` with the password from the database control panel
   (`db62052` → *Local access for websites* → **Show with password**). The file is gitignored; it holds
   that password and the token signing key.
2. **Turn on HTTPS** in the website control panel (*HTTPS certificates* → free Let's Encrypt). The host
   redirects http to https outside Development, and `KocApi:BaseUrl` — how the website reaches its own
   API surface — is an https URL. Without a certificate every one of those calls fails.
3. **Check the .NET runtime bitness.** The control panel shows `.NET 10.x … [x64]`, which is what
   `publish.ps1` builds for. If it is changed back to x86, publish with `-Runtime win-x86`.

## Every publish

```powershell
./deploy/smarterasp/publish.ps1
```

It builds Release, copies `appsettings.Production.json` into the output, and syncs to the site with
msdeploy, taking the app offline for the duration. `-SkipUpload` builds and stages without deploying.

Visual Studio's **Publish → Import Profile** on the `.publishSettings` file does the same upload, but it
does not copy `appsettings.Production.json` — do that by hand, or the site will not start.

## Why the settings file is required

The published binaries carry no `appsettings.Development.json`, so the host resolves to **Production**
(`KocHostEnvironment`), and `KocProductionPreflight` then refuses to start on a dev-shaped configuration.
Deploying without `appsettings.Production.json` gives an HTTP 500.30 and a startup exception naming what
is missing. Three settings in it are specific to IIS shared hosting:

| Setting | Why |
|---|---|
| `Platform:InternalPort=0` | The app normally opens a second loopback listener for `/api/v1` via `UseUrls`, which **IIS in-process hosting ignores** — leaving the website calling a port nothing listens on. `0` skips it and maps the surface on the site's own listener. |
| `Platform:InternalApiOnly=false` | With no loopback listener the website reaches its own API over the public address, so the request arrives from outside and the loopback-only guard would 404 it. The endpoints still require a bearer token. |
| `Auth:SignInWith=SiteAccounts` | `KocEnvironment` needs IIS to have authenticated the visitor against the KOC domain first, which shared hosting cannot do. |

## First run

`Database:MigrateOnStartup` builds the schema on the empty hosted database and puts the platform's
content in with it — the learning tracks and quizzes, the badge catalogue, the competition categories,
the workflow templates, and **the competitions with their training, evaluation and answer-key data**. The
site is complete on its first request; there is nothing to install afterwards.

All of that happens before the host serves anything, so give the first request longer than usual —
ASP.NET Core Module allows 120 seconds for startup, and this run generates every competition's datasets.
If it is not enough and you get a 500.30, raise it on the server in `web.config`
(`<aspNetCore startupTimeLimit="300" …>`) and restart; the second start has nothing left to do.

What the site does not have is **people**. **The first registration claims the platform as its
administrator** — so register immediately after the first successful deploy, before the address is
shared.

For a walkthrough, **/admin → Demo data** adds demonstration colleagues with submissions on the real
leaderboards, discussions and datasets, and removes them again afterwards without touching the
competitions.

## Known limits of this host

- **1 GB RAM, shared across every visitor.** Nothing here trains models — training belongs to KOC Studio
  on the desktop, and the server's training routes were removed in `96ceffa`. What consumes memory is
  Blazor Server: one live circuit per open tab, held server-side. That is the number to watch, and an
  app-pool recycle drops all of them at once.
- **Blazor Server needs WebSockets.** If they are off for the site, the circuit falls back to long
  polling and the leaderboard hub gets noticeably slower.
- **`Encrypt=False` on the connection string** is the host's own guidance — the website and the MSSQL
  server share their internal network. It would not be acceptable for a KOC-hosted deployment.
