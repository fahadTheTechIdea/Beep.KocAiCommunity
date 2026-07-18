# Phase 11 — Experiment Tracking and Evaluation

**Status:** 🟡 PLANNING
**Dependencies:** Phase 08, Phase 10
**Goal:** Native EF experiment tracking driven by ML.NET `IMonitor`, plus comparison, lineage, and an optional MLflow sink adapter.

## 1. Goal and dependencies

- Experiments, runs, trials, parameters, tags, metrics, snapshots, lineage
- ML.NET `IMonitor` adapter with nonblocking event channel
- Experiment comparison, best-run selection, filters, favorites, parent/child runs
- Task-specific visualizations (confusion matrix, ROC/PR, residuals, forecast, feature importance)
- Optional `IExperimentSink` abstraction for MLflow REST export

## 2. Existing reference behavior

- Beep.AI.MLStudio: `app/models/experiment.py:73-378` (wide experiment model with lineage).
- ML.NET AutoML: [AutoML API docs](https://learn.microsoft.com/en-us/dotnet/machine-learning/how-to-guides/how-to-use-the-automl-api) (IMonitor pattern).

## 3. Architecture decisions

| Decision | Choice | Rationale |
|---|---|---|
| Tracker | Native EF Core | No first-party .NET MLflow client |
| Monitor pattern | `IMonitor` → bounded channel → background writer | Non-blocking |
| Lineage | Parent/child run + workflow snapshot hash | Standard |
| Comparison | Run-level metric table; per-task views | Standard |
| MLflow | Optional `IExperimentSink` adapter | Pluggable |
| Visualization | Server-rendered SVG/PNG via SkiaSharp or chart library | Avoid JS bloat |

## 4. Project-by-project deliverables

### 4.1 Domain

- `Experiment`, `Run`, `RunMetric`, `RunParameter`, `RunTag`, `RunLog`
- `RunArtifact`, `RunSnapshot`

### 4.2 Application

- `IExperimentService`, `IRunService`, `IMetricService`
- DTO ↔ entity mapping
- `IExperimentSink` abstraction
- `IMlNetExperimentMonitor`

### 4.3 Infrastructure

- EF Core configurations
- `EfExperimentSink` writes to the experiment tables
- Optional `MlflowExperimentSink` (HTTP REST adapter)

### 4.4 ML

- `MlNetExperimentMonitor` writes `RunMetric` records into the bounded channel
- AutoML trial reporting

### 4.5 API

- Endpoints for experiments, runs, metrics, parameters, tags, snapshots, comparison

### 4.6 UI

- `Pages/Studio/Experiments/Index.razor`
- `Pages/Studio/Experiments/Detail.razor`
- `Pages/Studio/Experiments/Compare.razor`
- `Components/Studio/MetricsTable.razor`
- `Components/Studio/ConfusionMatrix.razor`
- `Components/Studio/RocCurve.razor`
- `Components/Studio/ResidualPlot.razor`
- `Components/Studio/ForecastTimeline.razor`
- `Components/Studio/FeatureImportance.razor`

## 5. Entities and migrations

```csharp
public class Experiment : AuditableEntity
{
    public string Name { get; set; } = default!;
    public string Description { get; set; } = default!;
    public string OwnerUserId { get; set; } = default!;
    public Guid? ProjectId { get; set; }
    public string Status { get; set; } = "active";
    public Guid? LatestBestRunId { get; set; }
    public string? Tags { get; set; }
}

public class Run : AuditableEntity
{
    public Guid ExperimentId { get; set; }
    public Guid? WorkflowId { get; set; }
    public Guid? WorkflowVersionId { get; set; }
    public Guid? DatasetId { get; set; }
    public Guid? DatasetVersionId { get; set; }
    public Guid? ParentRunId { get; set; }
    public string Status { get; set; } = "pending";
    public string? FailureStage { get; set; }
    public string? HyperparametersJson { get; set; }
    public string? EnvironmentJson { get; set; }
    public string? WorkflowSnapshotHash { get; set; }
    public string? DatasetSnapshotHash { get; set; }
    public string? DependencySnapshotJson { get; set; }
    public DateTime? StartedUtc { get; set; }
    public DateTime? CompletedUtc { get; set; }
    public bool IsFavorite { get; set; }
    public bool IsBest { get; set; }
}

public class RunMetric : AuditableEntity
{
    public Guid RunId { get; set; }
    public string Name { get; set; } = default!;
    public double Value { get; set; }
    public string? Dataset { get; set; }  // "train", "validation", "test"
    public string? Phase { get; set; }    // "epoch_1", "trial_2", etc.
    public int Step { get; set; }
    public DateTime LoggedUtc { get; set; }
}

public class RunParameter : AuditableEntity { /* RunId, Name, ValueJson */ }
public class RunTag : AuditableEntity { /* RunId, Key, Value */ }
public class RunLog : AuditableEntity { /* RunId, Severity, Message, LoggedUtc */ }
public class RunArtifact : AuditableEntity { /* RunId, ArtifactReferenceId, Type: model, plot, dataset */ }
public class RunSnapshot : AuditableEntity { /* RunId, Type: workflow, dataset, environment, dependency */ }
```

## 6. API contracts

```http
GET    /api/v1/experiments?projectId=&page=
POST   /api/v1/experiments
GET    /api/v1/experiments/{id}
PUT    /api/v1/experiments/{id}
DELETE /api/v1/experiments/{id}
GET    /api/v1/experiments/{id}/runs?page=
POST   /api/v1/experiments/{id}/runs
GET    /api/v1/runs/{id}
PUT    /api/v1/runs/{id} (favorite, best, tags)
GET    /api/v1/runs/{id}/metrics
POST   /api/v1/runs/{id}/metrics
GET    /api/v1/runs/{id}/parameters
POST   /api/v1/runs/{id}/parameters
GET    /api/v1/runs/{id}/logs?severity=&since=
POST   /api/v1/runs/{id}/logs
GET    /api/v1/runs/{id}/artifacts
POST   /api/v1/runs/{id}/artifacts
POST   /api/v1/experiments/{id}/compare
GET    /api/v1/experiments/{id}/best-run
```

## 7. MudBlazor pages and components

- All visualization pages use `MudChart` for line/bar and a custom SVG component for confusion matrix and ROC/PR

## 8. Security and authorization

- Project members can view experiments and runs
- Project owners and PlatformAdmin can edit runs (favorites, tags, best flag)
- Metric ingestion requires the run to be in `running` status

## 9. Tests

- Unit: monitor writes non-blocking, channel backpressure, batching
- Integration: experiment lifecycle, run lineage, metric ingestion, comparison
- Component: confusion matrix, ROC/PR, residual plot, forecast timeline

## 10. Verification commands

```bash
dotnet test tests/Beep.KocAiCommunity.IntegrationTests --filter "FullyQualifiedName~Experiments|FullyQualifiedName~Runs"
```

## 11. Acceptance gate

- Multiple AutoML trials persist live metrics
- Comparison results are reproducible
- Run lineage includes workflow, dataset, environment, and dependency snapshots
- `IMonitor` does not block ML.NET training
- `IExperimentSink` can be swapped to MLflow REST adapter (smoke test)
- Tests pass

## 12. Risks and deferred work

- MLflow adapter is optional and can ship later; the abstraction is the contract
- Visualization library choice (SkiaSharp server-side vs. client-side) needs benchmark
- Run metric cardinality must be controlled; document the rule
