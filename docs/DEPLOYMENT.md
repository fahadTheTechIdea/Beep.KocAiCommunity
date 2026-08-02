# Deployment

KocAiCommunity ships as three containers — **API**, **Web**, **Worker** — backed by **SQL Server**
(Azure SQL in production). Target hosting is the **Azure Kuwait Central** region for data residency.

## Container images

Built from the repository root (multi-stage; the API and Worker images add `libgomp1` for ML.NET):

```bash
docker build -f Dockerfile.api    -t koc-api    .
docker build -f Dockerfile.web    -t koc-web    .
docker build -f Dockerfile.worker -t koc-worker .
```

## Local production-shape stack

`docker compose up --build` starts SQL Server + all three services and opens a **seeded demo** on
SQL Server at <http://localhost:5150>. This uses dev auth and starter data — see the compose file's
header and the production section below before using it for anything real.

## Database migrations (provider-specific)

Migrations are provider-specific and live in two places:

| Provider | Migrations | Used when |
|---|---|---|
| **SQLite** (dev/test) | `Beep.KocAiCommunity.Infrastructure/Persistence/Migrations` | `Database:Provider=Sqlite` (default) |
| **SQL Server** (prod) | `Beep.KocAiCommunity.Infrastructure.SqlServerMigrations/Migrations` | `Database:Provider=SqlServer` (selected via `MigrationsAssembly`) |

Apply them one of two ways:

- **On startup** — set `Database:MigrateOnStartup=true` (production) or `Seed:Enabled=true` (dev). The
  correct provider's migrations are chosen automatically.
- **Explicitly** — `dotnet ef database update --project src/Beep.KocAiCommunity.Infrastructure.SqlServerMigrations --startup-project src/Beep.KocAiCommunity.Infrastructure.SqlServerMigrations --connection "<azure-sql-connection>"`.

Add a SQL Server migration after a model change:

```bash
dotnet ef migrations add <Name> \
  --project src/Beep.KocAiCommunity.Infrastructure.SqlServerMigrations \
  --startup-project src/Beep.KocAiCommunity.Infrastructure.SqlServerMigrations \
  --output-dir Migrations
```

(Also add the matching SQLite migration in the Infrastructure project for dev parity.)

## Environment resolution (dev vs production)

The same compiled binaries run everywhere; **what ships beside them** decides the environment. The API
and Web resolve the environment at startup (`KocHostEnvironment.Resolve()`) with this precedence:

1. An explicit **`ASPNETCORE_ENVIRONMENT`** / **`DOTNET_ENVIRONMENT`** always wins — the recommended
   production lever. Set it per target: IIS `<EnvironmentName>Production</EnvironmentName>` in the
   publish profile (`.pubxml` → `web.config`), Docker `ENV`, or an Azure App Service app setting.
2. If neither is set, the environment is **inferred from whether `appsettings.Development.json` shipped**
   next to the binaries: present ⇒ `Development`, absent ⇒ `Production`. Production publishes exclude
   that file (`<CopyToPublishDirectory>Never</CopyToPublishDirectory>`), so a deployed build resolves to
   Production automatically — no one has to remember to set a variable.

A **fail-fast preflight** (`KocProductionPreflight`) then refuses to start a Production host that is
still configured for dev/demo: `Database:Provider` not `SqlServer`, `Seed:Enabled=true`,
`DevAuth:Enabled=true`, or no real authentication (neither Entra nor Windows SSO). The dev **persona
switcher** is hidden outside Development.

## Configuration (environment variables)

Configuration binds from environment variables using the `__` separator.

| Key | Purpose |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` for real deployments (HTTPS redirect on; Swagger off) |
| `Database__Provider` | `SqlServer` in production |
| `ConnectionStrings__kocdb` | Azure SQL connection string (or a Key Vault reference) |
| `Database__MigrateOnStartup` | `true` to apply migrations at startup |
| `AzureAd__TenantId`, `AzureAd__ClientId`, `AzureAd__ClientSecret`, `AzureAd__Instance`, `AzureAd__Audience` | Microsoft Entra (KOC tenant). Presence of TenantId + ClientId switches auth from dev-fallback to real Entra (OIDC for Web, JWT for API) |
| `KocApi__BaseUrl` (Web) | Internal URL of the API |
| `Artifacts__RootPath` | Local artifact path (or configure the Azure Blob provider) |

## Database credentials & connection strings

The app switches provider by `Database:Provider` and reads `ConnectionStrings:kocdb`
(`Infrastructure/DependencyInjection.cs`). **Changing databases is configuration only — no code change.**
Where the credential lives depends on the environment; **prefer passwordless** so there is no secret to
store or rotate:

| Environment | Recommended approach | Secret stored? |
|---|---|---|
| **Local dev** | **User Secrets** — `dotnet user-secrets set "ConnectionStrings:kocdb" "…" --project src/Beep.KocAiCommunity.Web`. Active only in Development, never in source. Default stays SQLite (no config needed). | On the dev machine only |
| **On-prem IIS (intranet)** | **Windows Integrated auth** — run the app-pool as a dedicated Windows service account / **gMSA** granted a SQL login; connection string uses `Integrated Security=true` (no password). | **None** |


> **Moving from the old two-site layout.** The API was its own website and its own secret store
> (`beep-kocaicommunity-api`). Since it merged into the website on 2026-08-02 the host reads
> `beep-kocaicommunity-web` instead, so any connection string or signing key kept for the API has to be
> re-set. `dotnet user-secrets list --id beep-kocaicommunity-api` shows what was there; nothing is
> copied automatically.
| **Azure Kuwait Central** | **Entra Managed Identity** — the app's managed identity is granted a SQL user; connection string uses `Authentication=Active Directory Default` (no password). Any residual secret goes in **Key Vault**. | None (or Key Vault only) |

**How the connection string changes per environment** — config layering, highest wins, no rebuild:

```
appsettings.json                 no secrets; safe defaults (Provider defaults to Sqlite)
  → appsettings.{Environment}.json   non-secret overrides (Database:Provider=SqlServer, server/db host)
    → environment variables          ConnectionStrings__kocdb, injected by the IIS app-pool / container
      → Key Vault / User Secrets      the actual secret, if any
```

**Passwordless connection string examples** (no secret in any file):

```
# On-prem SQL Server via the app-pool's Windows identity
Server=KOC-SQL01;Database=Koc;Integrated Security=true;TrustServerCertificate=true

# Azure SQL via the app's Managed Identity
Server=tcp:koc.database.windows.net,1433;Database=Koc;Authentication=Active Directory Default;Encrypt=true
```

**Optional — Azure Key Vault** (cloud only; not needed for the on-prem passwordless path). Add the
`Azure.Extensions.AspNetCore.Configuration.Secrets` + `Azure.Identity` packages and, guarded by a
`KeyVault:Uri` setting, one line in host startup:

```csharp
if (builder.Configuration["KeyVault:Uri"] is { Length: > 0 } vault)
    builder.Configuration.AddAzureKeyVault(new Uri(vault), new DefaultAzureCredential());
```

Never commit a real connection string or password — today `appsettings.json` holds only logging and
`AllowedHosts`; keep it that way.

## Production checklist

- **Entra, not dev auth** — configure `AzureAd__*`. Ensure `DevAuth__Enabled` is unset/false and
  `Seed__Enabled` is false. Without Entra config and dev auth, protected endpoints correctly return 401.
- **Secrets from Key Vault** — reference the SQL connection string and `AzureAd__ClientSecret` via Key
  Vault; grant the app a **Managed Identity** with `get`/`list` on secrets. Prefer Managed Identity for
  Azure SQL and Blob storage over passwords/keys.
- **HTTPS at the ingress** — terminate TLS at the ingress/gateway; set `ASPNETCORE_ENVIRONMENT=Production`.
- **Data residency** — provision Azure SQL, Blob storage, and compute in **Azure Kuwait Central**; keep
  backups in the sovereign boundary.
- **Health** — each service exposes `/health` (readiness) and `/alive` (liveness) for probes.
- **Observability** — wire OpenTelemetry/App Insights via `ServiceDefaults` (the hook is in place).
- **Aspire** — for orchestrated environments, `src/Beep.KocAiCommunity.AppHost` composes the same
  services with health checks and service discovery.

## CI

`.github/workflows/ci.yml` gates every push/PR with restore → format check → build (warnings-as-errors)
→ test. Wire image build/push and deployment into the same pipeline per your registry and environment.
