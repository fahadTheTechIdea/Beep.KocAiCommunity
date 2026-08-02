# KOC Studio reads the platform database directly

**Decided 2026-08-02.** There is no API website any more. The platform surface — endpoints, hub, outbox
dispatcher — is a library (`Beep.KocAiCommunity.Platform`) that the website carries in-process, and
**KOC Studio on the desktop opens the same database itself** rather than calling the website.

## What changed

| Before | Now |
|---|---|
| `Beep.KocAiCommunity.Api`, its own website on :5250 | Deleted. Its code is the `Platform` library |
| Web → HTTP → API → database | Web → database, in one process |
| Desktop → HTTP → API → database | Desktop → database, in its own process |
| Two sites to deploy | One |

The desktop's `DesktopPlatformClient` calls the same application services the website's endpoints call.
Anything it does not implement throws by name rather than returning nothing, so a missing implementation
looks like a missing implementation and not like an empty platform.

## Configuring a workstation

`Settings → Platform database`. Blank is a valid, useful state: the machine works entirely offline —
datasets, the designer, training, run history, the local model registry — and only competitions and
experiments are unavailable, which the page says.

The connection is also in `%LOCALAPPDATA%\KocStudio\settings.json` as `PlatformDatabase`, with
`DatabaseProvider` (`SqlServer`, or `Sqlite` when pointed at a file for testing).

## The API is closed to the network

The website listens twice: the **public port** for pages, and a **loopback-only port** (default 5151)
where `/api/v1` is mapped. On the public port the API route does not exist — not refused, *unmatched* —
so there is nothing there to probe. The website calls its own internal listener; nothing else can reach
it without already being on the machine.

That surface had to be reachable when it was its own website: the site and KOC Studio both called it
across the network. Neither does now — the website calls it in-process over loopback, and the desktop
reads the database — so an API kept open would be attack surface maintained for nobody.

A second guard sits behind that: any `/api/v1` request whose caller is not on this machine gets a 404
rather than a 403, because a refusal tells whoever is probing that there is something to find. Belt and
braces, and the braces cost nothing.

Set `Platform:InternalPort` to `0` to drop the second listener (an in-process host has no ports, which
is how the test suite runs).

**The leaderboard hub (`/hubs/leaderboard`) is deliberately excluded** — KOC Studio still subscribes to
live standings from a workstation, so it is the one part of the surface with a real remote caller.

To reopen the API to the network for a deployment that reintroduces a remote client: set
`Platform:InternalPort` to `0` so it maps on the public port, and `Platform:InternalApiOnly` to `false`
so remote callers are not rejected.

## What the security team needs to know

This is the honest part, and it is a consequence of the decision rather than of how it was built.

**1. The workstation holds database credentials.** They are in the settings file under the user's
profile. Anyone who can read that file can read the connection string.

**2. The database must be reachable from workstations.** That is a firewall and network-exposure
question, not an application one.

**3. Authorization is enforced by the process asking.** On the website, a request carries a token the
website issued and role checks run against roles held in the database — about 120 policy checks on the
endpoints. On the desktop the identity is the signed-in Windows account, taken as a KOC employee,
because a KOC workstation is where the app runs. The desktop deliberately exposes **no administrative
surface**: no approvals, no user management, no competition hosting. But a determined person with the
connection string is not limited to what the app offers, because they can use the connection string
without the app.

**4. What this buys.** One deployment instead of two, and a desktop that keeps working when the website
is down. What it costs is the property that the database is only reachable by a service you control.

The mitigation that would restore that property is the one this replaced: the desktop talking to the
website over HTTP with a token. If the security review rejects direct access, that is the change to
make, and `RemoteFallbackKocApiClient` still supports it — pass an HTTP client instead of
`DesktopPlatformClient` and set `ApiBaseUrl`.

## Moving from the old two-site layout

- **User secrets do not carry over.** The API had its own store, `beep-kocaicommunity-api`; the website
  reads `beep-kocaicommunity-web`. Run `dotnet user-secrets list --id beep-kocaicommunity-api` and
  re-set anything you find.
- **`Seed:Enabled` moved** from the API's development settings into the website's. Without it a fresh
  clone starts against an empty, unmigrated database.
- **EF tooling now uses the website as its startup project.** See `docs/DEVELOPER_GUIDE.md`.
- **Migrations themselves did not move.** They were always in `Infrastructure/Persistence/Migrations`
  (SQLite) and `Infrastructure.SqlServerMigrations/Migrations`, and both hosts reference them.
