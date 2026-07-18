# Phase 07 — Datasets, Projects, and Collaboration

**Status:** 🟡 PLANNING
**Dependencies:** Phase 03, Phase 04
**Goal:** Datasets, ML projects, and collaboration workflows with KOC data classification enforcement.

## 1. Goal and dependencies

- Datasets with versions, files, schemas, licenses, tags, downloads, **org-scoped visibility** (Team/Group/Directorate/Company), lineage
- Dataset profiling (missing values, distributions, preview limits)
- ML projects with collaborators, snapshots, templates, activities
- Dataset-to-project import with classification enforcement
- File, URL, and SQL import adapters

## 2. Existing reference behavior

- Beep.AI.Community: `app/models/dataset.py`, `app/services/dataset_service.py`, `app/services/dataset_split_service.py`.
- Beep.AI.MLStudio: `app/models/project.py`, `app/models/dataset_downloads.py`.

## 3. Architecture decisions

| Decision | Choice | Rationale |
|---|---|---|
| Storage | Artifacts in `IArtifactStore`; metadata in EF | Per Phase 03 |
| Versioning | Immutable versions, draft → published | Provenance |
| License | SPDX expression stored per dataset | Industry standard |
| Classification | KOC info-sec levels enforced at download time | Compliance |
| Profiling | Sample-based (configurable row limit) | Performance |
| Imports | File, URL, SQL (no arbitrary script execution) | Security |
| Ownership | User + project owner | Standard |
| Visibility | `VisibilityScope` (Team/Group/Directorate/Company) + `VisibilityOrgUnitId`, chosen at creation | Creator picks "who can see this" along the KOC org tree; enforced by `IVisibilityEvaluator` (Phase 02) |

## 4. Project-by-project deliverables

### 4.1 Domain

- `Dataset`, `DatasetVersion`, `DatasetFile`, `DatasetSchema`, `DatasetLicense`
- `DatasetProfile`, `DatasetProfileColumn`
- `Project`, `ProjectMember`, `ProjectRole`, `ProjectActivity`, `ProjectTemplate`
- `DatasetImportJob`, `DatasetImportLog`

### 4.2 Application

- `IDatasetService`, `IProjectService`, `IDatasetImportService`, `IDatasetProfileService`
- DTO ↔ entity mapping
- Authorization helpers

### 4.3 Infrastructure

- EF Core configurations
- File import: streaming `IFormFile` upload, SHA-256, content sniff
- URL import: SSRF guard, content-type sniff, size limit, classification default
- SQL import: read-only connection (configurable statement timeout, no DDL), result to Parquet
- Profile engine: streamed CSV/Parquet reader, schema inference, summary stats

### 4.4 API

- Datasets, versions, files, schemas, profiles, imports
- Projects, members, activities, templates

### 4.5 UI

- `Pages/Datasets/Index.razor`, `Pages/Datasets/Detail.razor`, `Pages/Datasets/New.razor`, `Pages/Datasets/Profile.razor`
- `Pages/Projects/Index.razor`, `Pages/Projects/Detail.razor`, `Pages/Projects/New.razor`, `Pages/Projects/Settings.razor`
- `Components/Dataset/FilePicker.razor`, `Components/Dataset/SchemaEditor.razor`, `Components/Dataset/ProfileChart.razor`
- `Components/Project/MemberPicker.razor`, `Components/Project/ActivityFeed.razor`
- `Components/Shared/VisibilityScopePicker.razor` — the "who can see this" control: a Team/Group/Directorate/Company segmented selector bound to the creator's own org units, showing a live audience-count preview from `/api/v1/org/units/{id}/audience`. Reused by dataset, project, and competition create forms.

## 5. Entities and migrations

```csharp
public class Dataset : AuditableEntity
{
    public string Name { get; set; } = default!;
    public string Description { get; set; } = default!;
    public string OwnerUserId { get; set; } = default!;
    public VisibilityScope VisibilityScope { get; set; } = VisibilityScope.Team; // Team, Group, Directorate, Company
    public Guid VisibilityOrgUnitId { get; set; }   // which Team/Group/Directorate can see it; ignored for Company
    public KocDataClassification Classification { get; set; }
    public string? LicenseSpdxId { get; set; }
    public string? Tags { get; set; }
    public string? Domain { get; set; }  // upstream, midstream, downstream, hse
    public int LatestVersionNumber { get; set; }
}

public class DatasetVersion : AuditableEntity
{
    public Guid DatasetId { get; set; }
    public int VersionNumber { get; set; }
    public string Status { get; set; } = "draft"; // draft, published, archived
    public string? Notes { get; set; }
    public string? SourceConnectorType { get; set; }
    public string? SourceConnectorEntity { get; set; }
    public long TotalSizeBytes { get; set; }
    public string Sha256 { get; set; } = default!;
}

public class DatasetFile : AuditableEntity
{
    public Guid DatasetVersionId { get; set; }
    public Guid ArtifactReferenceId { get; set; }
    public string LogicalPath { get; set; } = default!;
    public string ContentType { get; set; } = default!;
    public long SizeBytes { get; set; }
}

public class DatasetSchema : AuditableEntity
{
    public Guid DatasetVersionId { get; set; }
    public string ColumnName { get; set; } = default!;
    public string DataType { get; set; } = default!;
    public bool Nullable { get; set; }
    public string? Description { get; set; }
}

public class DatasetProfile : AuditableEntity { /* datasetVersionId, sampleRowCount, generatedUtc */ }
public class DatasetProfileColumn { /* columnName, nullCount, distinctCount, min, max, mean, stdDev */ }

public class Project : AuditableEntity
{
    public string Name { get; set; } = default!;
    public string Description { get; set; } = default!;
    public string OwnerUserId { get; set; } = default!;
    public VisibilityScope VisibilityScope { get; set; } = VisibilityScope.Team;
    public Guid VisibilityOrgUnitId { get; set; }
    public KocDataClassification Classification { get; set; }
    public string Domain { get; set; } = "upstream";
    public string? Tags { get; set; }
    public Guid? SourceTemplateId { get; set; }
}

public class ProjectMember : AuditableEntity { /* ProjectId, UserId, Role: owner/contributor/viewer */ }
public class ProjectActivity : AuditableEntity { /* ProjectId, ActorUserId, Type, PayloadJson */ }
public class ProjectTemplate : AuditableEntity { /* Name, Description, PayloadJson */ }
```

## 6. API contracts

```http
GET    /api/v1/datasets?classification=&domain=&page=
POST   /api/v1/datasets
GET    /api/v1/datasets/{id}
PUT    /api/v1/datasets/{id}
DELETE /api/v1/datasets/{id}
POST   /api/v1/datasets/{id}/versions
GET    /api/v1/datasets/{id}/versions
POST   /api/v1/datasets/{id}/versions/{versionId}/publish
POST   /api/v1/datasets/{id}/versions/{versionId}/archive
GET    /api/v1/datasets/{id}/versions/{versionId}/files
POST   /api/v1/datasets/{id}/versions/{versionId}/files
GET    /api/v1/datasets/{id}/versions/{versionId}/files/{fileId}/download
POST   /api/v1/datasets/{id}/versions/{versionId}/profile
GET    /api/v1/datasets/{id}/versions/{versionId}/profile
POST   /api/v1/datasets/{id}/imports
GET    /api/v1/datasets/{id}/imports/{jobId}

GET    /api/v1/projects?domain=&classification=&page=
POST   /api/v1/projects
GET    /api/v1/projects/{id}
PUT    /api/v1/projects/{id}
DELETE /api/v1/projects/{id}
POST   /api/v1/projects/{id}/members
PUT    /api/v1/projects/{id}/members/{memberId}
DELETE /api/v1/projects/{id}/members/{memberId}
GET    /api/v1/projects/{id}/activity
POST   /api/v1/projects/{id}/datasets/{datasetId}
```

## 7. MudBlazor pages and components

- All dataset and project pages use MudBlazor components only; verified against `mudBlazor_Docs/`
- Profile charts use `MudChart` (bar/line/heatmap)

## 8. Security and authorization

- Classification enforced on download: Confidential and Restricted require explicit permission
- Visibility enforced by `IVisibilityEvaluator` (Phase 02): a dataset/project is visible only to users whose home org unit is within the chosen `VisibilityScope` subtree (`Team`/`Group`/`Directorate`), or to all KOC users when scope is `Company`. List endpoints filter by subtree at query time; the owner and explicit `UserEntityPermission` grants always retain access.
- At creation the "who can see this" selector defaults `VisibilityOrgUnitId` to the creator's own unit at the chosen level; a non-admin cannot pick a unit they do not belong to. `PlatformAdmin` may target any unit.
- Import URLs blocked to private IP ranges by default
- SQL imports use read-only connections (configurable statement timeout, no DDL)

## 9. Tests

- Unit: classification enforcement, license parsing, schema inference, profile stats
- Integration: dataset lifecycle, version immutability, profile generation, import jobs
- Integration: project membership and activity feed
- Component: dataset picker, profile chart, member picker

## 10. Verification commands

```bash
dotnet test tests/Beep.KocAiCommunity.IntegrationTests --filter "FullyQualifiedName~Datasets|FullyQualifiedName~Projects"
```

## 11. Acceptance gate

- Dataset version immutability enforced
- Classification enforced on download
- Org-scoped visibility enforced: a Team-scoped dataset is invisible to a peer in another Team; a Company-scoped dataset is visible to all KOC users; list endpoints filter by subtree
- Create form defaults the visibility unit to the creator's own and rejects a foreign unit for non-admins
- Project membership changes reflected in authorization checks immediately
- Profile generation reproducible with fixed seed
- Import URL SSRF guard works
- Tests pass

## 12. Risks and deferred work

- Massive datasets (>10 GB) require streaming profile; profile is sampled, not exhaustive
- Notebook execution remains disabled (asset storage and versioning only)
- Dataset-to-project import does not run AutoML yet; that is Phase 11
