# Phase 03 — EF Core Data and Artifact Storage

**Status:** 🟡 PLANNING
**Dependencies:** Phase 01
**Goal:** Establish the canonical `DbContext`, EF entity configurations, migrations for SQL Server and SQLite, and a pluggable artifact store.

## 1. Goal and dependencies

- One `KocDbContext` per provider (configurations live in `Infrastructure`)
- Two migration assemblies: `Infrastructure.SqlServerMigrations` and `Infrastructure.SqliteMigrations`
- Portable entity design (no SQL Server-only assumptions)
- Concurrency tokens that work on both providers
- `IArtifactStore` abstraction with Local filesystem and Azure Blob providers
- Upload size limits, extension allowlists, content inspection
- KOC data classification metadata

## 2. Existing reference behavior

- BeepWeb uses EF Core only for Identity (`Beep.Razor.Components/Data/ApplicationDbContext.cs:7-9`).
- Beep.AI.Community uses SQLAlchemy; multi-provider detected at `app/utils/database_provider.py:43-130`.

## 3. Architecture decisions

| Decision | Choice | Rationale |
|---|---|---|
| ORM | EF Core 10.0.10 | Per Phase 00 |
| Naming | PascalCase properties, snake_case columns via `UseSnakeCaseNamingConvention` only when needed | Convention over configuration |
| Primary keys | `Guid` (`uniqueidentifier`/`TEXT`) | Default for new codebases |
| Soft delete | Global query filter on `IsDeleted` | Recoverable deletes |
| Audit columns | `CreatedUtc`, `CreatedByUserId`, `LastModifiedUtc`, `LastModifiedByUserId` | Standard |
| Concurrency | `xmin` (PostgreSQL), `rowversion` (SQL Server), trigger for SQLite | Provider-specific |
| Migrations | Two assemblies, one per provider | DDL diverges |
| Schema | `dbo` for Identity, `koc` for application, `platform` for admin | Logical separation |
| Artifacts | `IArtifactStore`; Local + Azure Blob | Plug-in |

## 4. Project-by-project deliverables

### 4.1 Domain entities (this stage)

```csharp
public abstract class AuditableEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedUtc { get; set; }
    public string CreatedByUserId { get; set; } = default!;
    public DateTime LastModifiedUtc { get; set; }
    public string LastModifiedByUserId { get; set; } = default!;
    public bool IsDeleted { get; set; }
    public byte[] RowVersion { get; set; } = default!;  // populated by SaveChanges interceptor
}

public enum KocDataClassification { Public, Internal, Confidential, Restricted }
```

### 4.2 Infrastructure

- `KocDbContext` (with Identity tables)
- `KocDbContextDesignTimeFactory` for both providers
- EF Core configurations for each entity
- `AuditSaveChangesInterceptor` populates audit columns and concurrency tokens
- `IArtifactStore` and `LocalArtifactStore`, `AzureBlobArtifactStore`
- `ArtifactUploadOptions` with size and extension limits

### 4.3 SQL Server migrations

- Migration assembly `Infrastructure.SqlServerMigrations`
- All tables use `datetime2(7)` and `uniqueidentifier`
- `rowversion` concurrency token

### 4.4 SQLite migrations

- Migration assembly `Infrastructure.SqliteMigrations`
- `TEXT` primary keys and timestamps
- Concurrency via `xmin` simulation (counter column updated by trigger)

## 5. Entities and migrations

This stage seeds only the minimum viable schema:

- Identity tables (via `AddIdentity`)
- `AuditEvent` (audit envelope sink for Phase 14a)
- `UserEntityPermission` (from Phase 02)
- `ArtifactReference(Id, Container, Path, Sha256, SizeBytes, ContentType, Classification, OwnerUserId, CreatedUtc, IsDeleted)`

Subsequent stages add domain tables. Migrations are idempotent and reversible.

## 6. API contracts

```http
POST   /api/v1/artifacts/upload
GET    /api/v1/artifacts/{id}
GET    /api/v1/artifacts/{id}/download
DELETE /api/v1/artifacts/{id}
```

## 7. MudBlazor pages and components

- `Components/Shared/FileUpload.razor` (MudBlazor `MudFileUpload` with size and type filters)

## 8. Security and authorization

- All `/api/v1/artifacts/*` require Employee
- Classification enforcement: Confidential and Restricted require explicit download authorization
- Hash and content inspection: SHA-256 stored; content-type sniffed from the bytes (not just the client-supplied type)
- Extension allowlist per classification level

## 9. Tests

- Unit: configuration validation, save changes interceptor behavior, artifact hashing
- Integration: provider-specific migrations apply and roll back cleanly
- Integration: upload, download, delete with classification checks
- Architecture: no DbContext outside Infrastructure

## 10. Verification commands

```bash
dotnet ef database update --project src/Beep.KocAiCommunity.Infrastructure.SqlServerMigrations --startup-project src/Beep.KocAiCommunity.Api
dotnet ef database update --project src/Beep.KocAiCommunity.Infrastructure.SqliteMigrations --startup-project src/Beep.KocAiCommunity.Api
```

## 11. Acceptance gate

- Both migrations apply to fresh databases
- Migration rollback works
- Save changes interceptor populates audit columns
- Artifact upload, download, and delete work end-to-end
- Classification is enforced on downloads
- Content sniffing rejects mismatched MIME types
- Tests pass on both providers

## 12. Risks and deferred work

- Concurrency tokens need provider-specific handling; centralize in the interceptor
- The trigger-based SQLite concurrency is fragile if the schema is hand-edited; document the rule
- Artifact upload limits are configurable per stage; defaults are conservative (50 MB, common file types only)
