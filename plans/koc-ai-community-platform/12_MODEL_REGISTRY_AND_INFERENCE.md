# Phase 12 — Model Registry and Inference

**Status:** ✅ DONE (2026-07-19)
**Dependencies:** Phase 11
**Goal:** Model artifact registry with semantic versions, lifecycle states, prediction pools, and protected inference endpoints.

## Implementation notes (2026-07-19)

The registry half (register → approve×2 → promote → deploy/rollback) already shipped. This session
closed the **inference** half end-to-end:

- **Model persistence.** Training now captures the winning model. `IMlTrainer.TrainAndCaptureAsync`
  returns the serialized ML.NET `.zip` bytes plus a drift baseline (per-numeric-feature
  count/mean/min/max). `StudioService` stores the model as a governed artifact
  (`IArtifactService`, classification `Internal`) and records `ModelRun.ModelArtifactId` +
  `ModelRun.FeatureStatsJson`. Every trained run (sync Studio or out-of-band `model.train` job) is
  now servable.
- **Prediction pool.** `IPredictionPool` (Application) + `AutoMlPredictionPool` (ML project,
  singleton). It caches the loaded `ITransformer` + input schema per model-version id, hot-reloads,
  and is evicted on retire/rollback. **Design decision:** platform models are trained on arbitrary
  user CSV schemas, so ML.NET's compile-time-typed `PredictionEnginePool<TSrc,TDst>` cannot be used.
  The pool instead scores rows *dynamically* against each model's own saved input schema (the label
  column is filled with a type-appropriate placeholder). Documented here so a future typed-pool
  path (per-template O&G models) is a deliberate follow-up, not an omission.
- **Protected inference.** `IInferenceService` + `InferenceService` (Infrastructure): online
  (`/infer`) and batch (`/infer/batch`) scoring, a `ModelInferenceLog` per call (caller, endpoint,
  row count, latency, success/error) — logged even on failure — and drift comparison (`/drift`,
  batch feature means vs the training baseline). Authz: production versions are servable to any
  employee; non-production versions only to the owner or a `PlatformAdmin`.
- **API/UI/client.** `/api/v1/models/versions/{id}/infer|infer/batch|inference-logs|drift`
  endpoints; typed `KocApiClient` methods; a `Predict` action on production versions in
  `Models.razor` opening an `InferenceDialog` (single-row scoring + drift check).
- **Migrations.** Dual-provider `AddInference` (SQLite + SqlServer) — new `ModelInferenceLogs` table
  + two `ModelRuns` columns.
- **Tests.** 2 unit (`PredictionPoolTests`: dynamic scoring + cache/evict) + 2 integration
  (`InferenceEndpointsTests`: promoted-version serving/logs/drift and non-production owner-only).
  AutoML-training unit classes serialized via `MlTrainingCollection` to avoid parallel resource
  contention. Whole solution builds `-warnaserror` clean; 87 unit + 53 integration tests pass.

**Deferred:** compile-time-typed `PredictionEnginePool<T>` for fixed O&G templates; continuous
drift monitoring (baseline persisted; comparison is on-demand per batch today); model signing via
KOC CA/KMS; the multi-page `Pages/Studio/Models/*` structure (single `Models.razor` + dialog today).

## 1. Goal and dependencies

- Register model artifacts with semantic versions and SHA-256 checksums
- Lifecycle states: staging, production, archived, rejected
- Microsoft.Extensions.ML prediction pools
- Protected batch and online inference endpoints
- Promotion approvals and rollback
- Latency metrics and drift metadata

## 2. Existing reference behavior

- Beep.AI.MLStudio: `app/services/ml_server/client.py:27-441` (publish to ML Server).
- Beep.AI.Community: `app/models/model_registry.py`, `app/services/model_registry_service.py`.
- ML.NET Model Builder: [Model Builder docs](https://learn.microsoft.com/en-us/dotnet/machine-learning/automate-training-with-model-builder) (model consumption).

## 3. Architecture decisions

| Decision | Choice | Rationale |
|---|---|---|
| Versioning | SemVer (major.minor.patch) | Industry standard |
| Lifecycle | staging / production / archived / rejected | Standard |
| Prediction pool | Microsoft.Extensions.ML `PredictionEnginePool` | Thread-safe, hot-reload |
| Inference API | Batch (POST JSON) + online (POST single record) | Standard |
| Promotion | Two-person rule for production; rollback available | KOC requirement |
| Trust | Model loaded only if signed and approved | Standard |
| Drift | Baseline dataset comparison at promotion time | Standard |

## 4. Project-by-project deliverables

### 4.1 Domain

- `Model`, `ModelVersion`, `ModelPromotion`, `ModelApproval`, `ModelInferenceLog`

### 4.2 Application

- `IModelService`, `IModelRegistryService`, `IInferenceService`
- DTO ↔ entity mapping

### 4.3 Infrastructure

- EF Core configurations
- Model loading with signature verification
- Prediction pool per model version

### 4.4 API

- Model CRUD, promotion, approval, inference endpoints

### 4.5 UI

- `Pages/Studio/Models/Index.razor`
- `Pages/Studio/Models/Detail.razor`
- `Pages/Studio/Models/Versions.razor`
- `Pages/Studio/Models/Promote.razor`
- `Pages/Studio/Models/Inference.razor`
- `Components/Studio/ModelMetrics.razor`
- `Components/Studio/PromotionApproval.razor`

## 5. Entities and migrations

```csharp
public class Model : AuditableEntity
{
    public string Name { get; set; } = default!;
    public string Description { get; set; } = default!;
    public string OwnerUserId { get; set; } = default!;
    public Guid? ProjectId { get; set; }
    public Guid? ExperimentId { get; set; }
    public KocDataClassification Classification { get; set; }
    public string? Tags { get; set; }
    public string TaskType { get; set; } = default!;
}

public class ModelVersion : AuditableEntity
{
    public Guid ModelId { get; set; }
    public string SemVer { get; set; } = default!;  // 1.2.3
    public string Status { get; set; } = "staging";  // staging, production, archived, rejected
    public Guid ArtifactReferenceId { get; set; }
    public string Sha256 { get; set; } = default!;
    public string? SignatureJson { get; set; }
    public Guid? SourceRunId { get; set; }
    public DateTime? PromotedUtc { get; set; }
    public string? PromotedByUserId { get; set; }
    public DateTime? ArchivedUtc { get; set; }
    public string? MetricsJson { get; set; }
    public string? DriftBaselineJson { get; set; }
}

public class ModelApproval : AuditableEntity
{
    public Guid ModelVersionId { get; set; }
    public string Decision { get; set; } = default!;  // approve, reject
    public string ApproverUserId { get; set; } = default!;
    public string? Notes { get; set; }
}

public class ModelInferenceLog : AuditableEntity
{
    public Guid ModelVersionId { get; set; }
    public string CallerUserId { get; set; } = default!;
    public string Endpoint { get; set; } = default!;
    public int LatencyMs { get; set; }
    public DateTime CalledUtc { get; set; }
    public bool Success { get; set; }
    public string? ErrorJson { get; set; }
}
```

## 6. API contracts

```http
GET    /api/v1/models?projectId=&classification=&page=
POST   /api/v1/models
GET    /api/v1/models/{id}
PUT    /api/v1/models/{id}
DELETE /api/v1/models/{id}
GET    /api/v1/models/{id}/versions
POST   /api/v1/models/{id}/versions
GET    /api/v1/models/{id}/versions/{semVer}
POST   /api/v1/models/{id}/versions/{semVer}/promote
POST   /api/v1/models/{id}/versions/{semVer}/archive
POST   /api/v1/models/{id}/versions/{semVer}/rollback
GET    /api/v1/models/{id}/versions/{semVer}/approvals
POST   /api/v1/models/{id}/versions/{semVer}/approvals
POST   /api/v1/models/{id}/versions/{semVer}/infer
POST   /api/v1/models/{id}/versions/{semVer}/infer/batch
GET    /api/v1/models/{id}/versions/{semVer}/inference-logs
```

## 7. MudBlazor pages and components

- All model pages use MudBlazor; inference forms use `MudForm` with `MudTextField` arrays

## 8. Security and authorization

- Project members can view models
- Project owners and PlatformAdmin can promote/archive
- Production promotion requires two distinct approvals
- Inference endpoints log caller, latency, and result metadata
- Model trust: signed artifact, classification compatible with caller role

## 9. Tests

- Unit: semver validation, promotion state machine, prediction pool lifecycle
- Integration: model lifecycle, promotion, rollback, inference
- Component: inference form, promotion approval flow

## 10. Verification commands

```bash
dotnet test tests/Beep.KocAiCommunity.IntegrationTests --filter "FullyQualifiedName~Models"
```

## 11. Acceptance gate

- Model can move from experiment to registry to inference and safely roll back
- Promotion requires two approvals
- Inference logs capture latency and outcome
- Classification is enforced on inference
- Tests pass

## 12. Risks and deferred work

- ML.NET 5 introduced some API changes around `PredictionEnginePool`; verify compatibility
- Drift detection requires a baseline dataset per model version
- Model signing uses KOC certificate authority; integration with KMS is a follow-up
