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
