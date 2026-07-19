# Phase 07a — KOC Enterprise Connectors

**Status:** 🟢 MOSTLY DONE (2026-07-19) — abstractions, catalog, instances, encrypted credential vault, mock adapters, and health snapshots shipped; live PPDM/SAP/PI/… adapters are deployment-time.
**Dependencies:** Phase 03, Phase 04, Phase 07
**Goal:** First-class connectors for PPDM 39, OpenWells, EcoSys, SAP, AVEVA PI, and ADLS Gen2 with credential vault, classification, and health monitoring.

## Implementation notes (2026-07-19)

Real PPDM/OpenWells/EcoSys/SAP/PI/ADLS endpoints aren't reachable in this environment, so — as the plan
itself notes (§12) — **mock adapters are the staging default**. The full contract + persistence + vault
are real; only the six live adapters are stubbed.

- **Abstractions.** `IKocConnector` (Test / GetSchema / Health), `ConnectorContext`, `ConnectorSchema`/
  `ConnectorResource`/`ConnectorColumn`, `ConnectorTestResult`, `ConnectorHealthResult`,
  `IKocConnectorFactory`, and a code-first `ConnectorCatalog` (the six connectors with default
  classification — PI Restricted, PPDM/OpenWells/SAP Confidential, EcoSys/ADLS Internal — auth modes,
  and capabilities).
- **Persistence.** `ConnectorInstance` (endpoint, auth mode, default classification, probe interval),
  `CredentialVaultEntry` (encrypted at rest via `ISecretProtector`), `ConnectorHealthSnapshot`. Dual-
  provider `AddConnectors` migration (schema `koc`/`platform`).
- **Service.** `ConnectorService` — instance CRUD, **SSRF-guarded** endpoint validation (reuses
  `UrlImportGuard` for http(s) endpoints), credential set/rotate (encrypted, never returned) + delete,
  and test/schema/**health-with-snapshot** via the factory. Every mutation is audited; secrets never
  appear in responses or audit JSON.
- **Mock adapter.** `MockConnector`/`MockConnectorFactory` return a code-appropriate fake schema
  (PPDM → WELL/WELLBORE/PDEN_VOL_SUMMARY, PI → AF tags, …) and report healthy — deterministic, offline.
  Real adapters implement the same `IKocConnector` and are swapped in at deployment.
- **API/UI.** `/api/v1/connectors/**` (catalog, instances, credentials, test/schema/health) all behind
  `RequirePlatformAdmin`; typed client; a `/connectors` admin page.
- **Tests.** 3 unit (`ConnectorCatalogTests`) + 4 integration (`ConnectorEndpointsTests`: admin-only,
  catalog, SSRF-blocked endpoint, full lifecycle with the secret never exposed in responses or audit).

**Deferred (deployment-time):** the six live adapters (PPDM SQL, OpenWells/EcoSys REST, SAP RFC, PI Web
API, ADLS Gen2); the scheduled connector health-monitor hosted service in the Worker (health is
on-demand today); connector→dataset import jobs with lineage; Key Vault credential references in prod.

## 1. Goal and dependencies

- Connector abstractions and adapter pattern
- Per-connector credential vault entries (Data Protection in dev, Key Vault references in production)
- Browse, schema introspection, and dataset creation from each connector
- Connector health monitoring feeding the admin dashboard
- Per-connector classification default and escalation policy

## 2. Existing reference behavior

- Beep.AI.Community has `kaggle_adapter.py`, `dataset_marketplace_service.py`.
- Beep.AI.Server has `app/clients/identity_server_client.py:411` (HTTP client guardrails).

## 3. Architecture decisions

| Decision | Choice | Rationale |
|---|---|---|
| Pattern | `IKocConnector` interface + per-source adapters | Standard |
| Credentials | `CredentialVaultEntry` table, encrypted column | Per Phase 03 |
| Auth modes | Basic, OAuth2 client credentials, certificate, integrated | KOC standards |
| Schema | Connector-specific schema enumeration | Standard |
| Read-only mode | Default for SQL and SAP connectors | KOC requirement |
| Health | Per-connector health probe (interval, timeout configurable) | Reliability |
| Classification | Per-connector default classification, override per resource | Compliance |

## 4. Project-by-project deliverables

### 4.1 Connectors.Abstractions

- `IKocConnector`
- `ConnectorCapabilities`
- `ConnectorResourceDescriptor`
- `ConnectorSchema`
- `ConnectorHealthResult`
- `ConnectorTestResult`
- `ICredentialVault`

### 4.2 Connectors.PPDM

- PPDM 39 schema introspection (well, wellbore, log, production, etc.)
- Read-only SQL access with paging
- Default classification: Confidential

### 4.3 Connectors.OpenWells

- REST adapter (OpenWells Activity API)
- OAuth2 client credentials or API token
- Default classification: Confidential

### 4.4 Connectors.EcoSys

- REST adapter (EcoSys Project Server API)
- Project, portfolio, schedule entities
- Default classification: Internal

### 4.5 Connectors.Sap

- RFC/BAPI gateway for PM/MM modules
- Read-only access
- Default classification: Confidential

### 4.6 Connectors.Pi

- AVEVA PI Web API
- AF database and tag queries
- Time-series pull into dataset versions
- Default classification: Restricted

### 4.7 Connectors.AdlsGen2

- Azure Data Lake Storage Gen2
- Service principal or shared key
- Default classification: Internal

### 4.8 Connectors catalog

- Connector factory `IKocConnectorFactory` returns connectors by name
- Connector health monitor hosted service in Worker
- Connector UI under `/connectors`

## 5. Entities and migrations

```csharp
public class ConnectorDefinition : AuditableEntity
{
    public string Code { get; set; } = default!;  // ppdm, openwells, ecosys, sap, pi, adls
    public string DisplayName { get; set; } = default!;
    public string Version { get; set; } = default!;
    public string CapabilitiesJson { get; set; } = default!;
}

public class ConnectorInstance : AuditableEntity
{
    public Guid ConnectorDefinitionId { get; set; }
    public string Name { get; set; } = default!;
    public string Endpoint { get; set; } = default!;
    public string AuthMode { get; set; } = default!;
    public string? AuthConfigJson { get; set; }
    public KocDataClassification DefaultClassification { get; set; }
    public bool IsEnabled { get; set; }
    public int HealthProbeIntervalSeconds { get; set; } = 60;
}

public class CredentialVaultEntry : AuditableEntity
{
    public Guid ConnectorInstanceId { get; set; }
    public string Key { get; set; } = default!;
    public byte[] EncryptedValue { get; set; } = default!;
    public string ProtectionDescriptor { get; set; } = default!;  // DataProtection or KeyVault
    public DateTime LastRotatedUtc { get; set; }
    public DateTime? ExpiresUtc { get; set; }
}

public class ConnectorHealthSnapshot : AuditableEntity
{
    public Guid ConnectorInstanceId { get; set; }
    public string Status { get; set; } = default!;
    public int LatencyMs { get; set; }
    public string? DetailJson { get; set; }
    public DateTime MeasuredUtc { get; set; }
}
```

## 6. API contracts

```http
GET    /api/v1/connectors
GET    /api/v1/connectors/{code}/instances
POST   /api/v1/connectors/{code}/instances
GET    /api/v1/connectors/instances/{id}
PUT    /api/v1/connectors/instances/{id}
DELETE /api/v1/connectors/instances/{id}
POST   /api/v1/connectors/instances/{id}/test
GET    /api/v1/connectors/instances/{id}/schema
GET    /api/v1/connectors/instances/{id}/resources?path=
POST   /api/v1/connectors/instances/{id}/import
GET    /api/v1/connectors/instances/{id}/health
POST   /api/v1/connectors/instances/{id}/credentials
PUT    /api/v1/connectors/instances/{id}/credentials/{key}
DELETE /api/v1/connectors/instances/{id}/credentials/{key}
```

## 7. MudBlazor pages and components

- `Pages/Connectors/Index.razor` (catalog grid)
- `Pages/Connectors/Instance.razor` (configuration, schema browser, test)
- `Pages/Connectors/Health.razor` (per-instance health history)
- `Components/Connector/SchemaTree.razor`
- `Components/Connector/CredentialEditor.razor`

## 8. Security and authorization

- PlatformAdmin required to create or modify connector instances
- Credentials encrypted with Data Protection in dev, Key Vault references in production
- Health probe uses a dedicated, read-only service principal where applicable
- SSRF guard for endpoint validation (reject private IPs by default; per-connector override)
- All connector operations logged in the audit envelope

## 9. Tests

- Unit: credential encryption, schema enumeration, classification default
- Integration: each connector tested against a sandbox or a mocked equivalent
- Integration: import job creates dataset version with correct lineage
- Component: connector instance form, schema tree, credential editor

## 10. Verification commands

```bash
dotnet test tests/Beep.KocAiCommunity.IntegrationTests --filter "FullyQualifiedName~Connectors"
```

## 11. Acceptance gate

- Each connector passes integration tests against a sandbox or a mocked equivalent
- Credentials are encrypted at rest
- Schema introspection works
- Import creates a new dataset version with provenance metadata
- Health probe runs on schedule and writes `ConnectorHealthSnapshot`
- Tests pass

## 12. Risks and deferred work

- Real PPDM/SAP/PI connectivity is the longest lead time; mock adapters are the staging default
- ADLS Gen2 SAS rotation requires Key Vault integration in production
- AVEVA PI time-series ingestion can produce very large datasets; profile is sampled, not exhaustive
