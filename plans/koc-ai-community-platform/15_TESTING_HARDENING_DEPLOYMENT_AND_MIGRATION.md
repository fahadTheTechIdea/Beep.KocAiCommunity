# Phase 15 — Testing, Hardening, Deployment, and Migration

**Status:** 🟡 PLANNING
**Dependencies:** All previous stages
**Goal:** Comprehensive test matrix, CI gates, security hardening, Azure deployment in the Kuwait region, and migration from Python sources.

## 1. Goal and dependencies

- Unit, integration, API, EF-provider, Worker, ML-quality, component, Playwright, accessibility, architecture tests
- Security tests for authorization bypass, uploads, path traversal, SSRF, archive bombs, model trust
- Performance tests for workflow canvas, dataset preview, SignalR, queue throughput, inference
- CI gates: restore, format, build warnings-as-errors, unit, component, integration, end-to-end, dependency, vulnerability scans
- Deploy to Azure Kuwait region with Azure SQL and Azure Blob Storage
- Managed Identity and Key Vault references
- Backup, restore, migration, rollback, disaster recovery procedures
- Optional import of compatible metadata from Python Community and MLStudio SQLite

## 2. Existing reference behavior

- Beep.AI.Server: `tests/` (pytest-based, 100+ files).
- Beep.AI.MLStudio: `tests/` (21 pytest files).

## 3. Architecture decisions

| Decision | Choice | Rationale |
|---|---|---|
| Target framework | .NET 10 | LTS |
| CI runner | GitHub Actions | Standard |
| Deployment target | Azure App Service (Web), Azure Container Apps (API, Worker) | KOC standard |
| Region | Kuwait Central if available; UAE North fallback | Sovereign boundary |
| Database | Azure SQL with elastic pool | KOC standard |
| Blob | Azure Blob Storage with GRS in Kuwait | Sovereign boundary |
| Identity | Managed Identity + Key Vault | Standard |
| CI/CD | Build → test → package → deploy | Standard |

## 4. Project-by-project deliverables

### 4.1 Tests

```
tests/
├── Beep.KocAiCommunity.UnitTests/
├── Beep.KocAiCommunity.IntegrationTests/
├── Beep.KocAiCommunity.ComponentTests/
├── Beep.KocAiCommunity.ArchitectureTests/
├── Beep.KocAiCommunity.EndToEndTests/
├── Beep.KocAiCommunity.SecurityTests/
├── Beep.KocAiCommunity.PerformanceTests/
└── Beep.KocAiCommunity.MigrationTests/
```

- `SecurityTests`: OWASP-aligned tests (auth bypass, file upload, SSRF, archive bomb, model trust, secret exposure)
- `PerformanceTests`: Workflow canvas render, dataset preview, SignalR latency, queue throughput, inference latency
- `MigrationTests`: Migration upgrade paths for both providers

### 4.2 CI/CD

- `.github/workflows/build.yml` — restore, format, build, test on every push
- `.github/workflows/release.yml` — package, deploy to staging on `main`
- `.github/workflows/manual-deploy.yml` — manual promotion to production

### 4.3 Infrastructure as Code

- `infra/main.bicep` — Azure resources
- `infra/sql.bicep` — Azure SQL server + database + elastic pool
- `infra/blob.bicep` — Storage account + containers
- `infra/keyvault.bicep` — Key Vault with managed identity access policies

### 4.4 Operations

- `runbooks/incident-response.md`
- `runbooks/backup-restore.md`
- `runbooks/disaster-recovery.md`
- `runbooks/rollback-procedure.md`

### 4.5 Migration tooling

- `tools/PythonMetadataImporter` — reads SQLite from Beep.AI.Community / Beep.AI.MLStudio and emits JSON for import
- `tools/ImportRunner` — applies the JSON to a fresh KOC database

## 5. Entities and migrations

No new entities in this stage. Migration tests verify existing migrations apply, upgrade, and roll back.

## 6. API contracts

```http
GET  /api/v1/admin/operations/backup
POST /api/v1/admin/operations/restore
POST /api/v1/admin/operations/disaster-recovery-test
GET  /api/v1/admin/operations/import/status
POST /api/v1/admin/operations/import
```

## 7. MudBlazor pages and components

- `Pages/Admin/Operations.razor` (backup, restore, import)
- `Components/Admin/BackupStatus.razor`

## 8. Security and authorization

- All operations endpoints require PlatformAdmin
- Restore requires two-person approval
- Disaster recovery test runs in a sandbox database
- Import is read-only on the source

## 9. Tests

- Comprehensive matrix per `references/TEST_AND_ACCEPTANCE_MATRIX.md`
- Penetration tests cover OWASP Top 10
- Disaster recovery tests in sandbox

## 10. Verification commands

```bash
dotnet restore
dotnet format --verify-no-changes
dotnet build --no-restore -warnaserror
dotnet test --no-build

# Security
dotnet run --project tools/SecuritySmoke

# Performance
dotnet run --project tools/PerfHarness -- --scenario workflow-canvas
```

## 11. Acceptance gate

- All tests pass on both providers
- All CI gates pass
- Security tests show no high-severity issues
- Performance benchmarks within budget
- Staging deployment passes smoke, security, migration, backup/restore, rollback exercises
- Disaster recovery test passes

## 12. Risks and deferred work

- Kuwait Central availability must be verified at deployment time
- Migration from Python sources is read-only and best-effort; manual review required
- Cross-region failover outside Kuwait sovereign boundary is explicitly out of scope
