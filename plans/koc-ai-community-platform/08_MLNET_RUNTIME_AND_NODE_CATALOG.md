# Phase 08 — ML.NET Runtime and Node Catalog

**Status:** 🟢 MOSTLY DONE (2026-07-19) — node abstractions, O&G catalog, featurization guard, and determinism shipped; deeper per-task handlers (anomaly/forecasting execution) deferred.
**Dependencies:** Phase 03, Phase 07, Phase 07a
**Goal:** Define the ML.NET runtime contracts, register the O&G node catalog, and validate deterministic sample workflows.

## Implementation notes (2026-07-19)

The ML.NET executor (`MlPipelineExecutor`) and AutoML (`AutoMlTrainer`, scoped `MLContext`, fixed seed)
shipped earlier. This session added the **formal node layer + guardrails**:

- **Node abstractions (code-first).** `Application/ML/NodeCatalog.cs`: `NodeDescriptor` (kind, category,
  ports, typed `NodeParameter`s), `INodeRegistry` with lookup + `ValidateParameters` (required / numeric
  / select-option checks), and `MlNodeRegistry` — the canonical, O&G-flavored catalog of all 22 pipeline
  kinds. A unit test asserts every catalog kind is known to `WorkflowCompiler`, keeping backend and
  compiler in sync.
- **ML task catalog.** `MlTaskCatalog` advertises the three AutoML-backed tasks (binary/multiclass/
  regression, `MlTaskType`-mapped, `Supported = true`) plus anomaly + forecasting as declared-but-not-yet-
  executable (`Supported = false`), each with an O&G example.
- **Split-before-fit guard.** `FeaturizationGuard.Check(WorkflowDefinition)` BFS's the graph and flags any
  supervised model (`train`/`cross-validate`) reachable from a dataset without a `split` in between —
  i.e. fitting on the train+test union (leakage). Unsupervised `cluster` is exempt. **Enforced on workflow
  publish** (`WorkflowVersionService.PublishAsync`) after the compiler, so a leaky graph can't be frozen.
- **Determinism.** A unit test confirms two AutoML runs on identical data + fixed seed produce identical
  winning algorithm and metrics.
- **API/UI/client.** `GET /api/v1/ml/nodes[/{kind}]`, `POST /ml/nodes/{kind}/validate`, `GET /ml/tasks`,
  `POST /ml/workflows/featurization-check`; typed client; a `/nodes` catalog page (tasks + nodes by
  category). No entities → no migration.
- **Tests.** 9 unit (`NodeCatalogTests`, `FeaturizationGuardTests`, `MlDeterminismTests`) + 3 integration
  (`MlNodeEndpointsTests`: catalog + validation, task catalog + featurization-check, publish rejects a
  no-split workflow). Whole solution builds `-warnaserror` clean; 115 unit + 76 integration tests pass.

**Deferred (documented):** executable anomaly-detection and time-series forecasting handlers (declared in
the task catalog, `Supported=false`); the full `IMlTaskHandler`/`INodeExecutor` per-node execution split
(the existing `MlPipelineExecutor` runs graphs today); Recommendation/ranking and TorchSharp deep learning.

## 1. Goal and dependencies

- `IMlRuntime`, `IMlTaskHandler`, `INodeDescriptor`, `INodeExecutor`
- ML.NET integration with MLContext lifetime management (scoped)
- O&G-specific node catalog (production rate forecasting, reservoir analytics, HSE anomaly, predictive maintenance)
- AutoML trials for binary classification, multiclass classification, and regression
- Strict featurization ordering: split before fit; missing-value handling; fixed seeds; temporal splits

## 2. Existing reference behavior

- Beep.ML.NET: `ML.NET/ML.NET.csproj`, `ML.NET/MLNetManager.cs`, `ML.NET.Modules/IMLNETDataManager.cs`.
- Beep.AI.Shared: `IMLTrain`, `IMLPredict`, `IMLEval`, `ITrainedModel` interfaces.
- ML.NET AutoML: [ML.NET AutoML API docs](https://learn.microsoft.com/en-us/dotnet/machine-learning/how-to-guides/how-to-use-the-automl-api).
- ML.NET Model Builder: [Model Builder docs](https://learn.microsoft.com/en-us/dotnet/machine-learning/automate-training-with-model-builder).

## 3. Architecture decisions

| Decision | Choice | Rationale |
|---|---|---|
| Runtime | Microsoft.ML 5.0.0 | Latest stable |
| MLContext lifetime | Scoped per request/circuit | Avoids thread-safety issues; matches ML.NET guidance |
| AutoML | Microsoft.ML.AutoML 0.23.0 | Stable, supports binary/multiclass/regression |
| Node model | Code-first registry, JSON-serialized | Standard |
| Featurization | Always split before fit; standardized encoders | Per ML best practices |
| Missing values | Analyzed; imputed or kept depending on context | Per ML best practices |
| Determinism | Fixed seed by default | Reproducibility |
| Time series | Chronological split, no shuffling | Per ML best practices |
| Metrics | Task-appropriate (accuracy/F1 for classification, RMSE/RSquared for regression) | Standard |

## 4. Project-by-project deliverables

### 4.1 Application/ML

- `IMlRuntime` (factory for `MLContext`)
- `IMlTaskHandler` (per-task implementations: BinaryClassification, MulticlassClassification, Regression, Recommendation, Forecasting, AnomalyDetection)
- `INodeDescriptor` (id, category, displayName, description, ports, parameters)
- `INodeExecutor` (ExecuteAsync returns output dataset binding)
- `INodeRegistry` (registration, lookup)
- `NodeParameter` (typed parameter definitions)

### 4.2 ML

- `MlRuntime` implementation
- Per-task handlers
- O&G node implementations
- AutoML monitor implementing `Microsoft.ML.AutoML.IMonitor`
- Per-ml-best-practices guardrails

### 4.3 Domain

- `MlNodeCategory` enum
- `MlTaskType` enum

## 5. Entities and migrations

None in this stage. Ml-related entities land in Phase 11 (experiments).

## 6. API contracts

```http
GET    /api/v1/ml/nodes
GET    /api/v1/ml/nodes/{id}
POST   /api/v1/ml/nodes/{id}/validate  (validate parameters)
```

## 7. MudBlazor pages and components

- `Pages/Studio/NodeCatalog.razor` (browse nodes by category)
- `Components/Studio/NodeCard.razor`
- `Components/Studio/NodeParameterEditor.razor`

## 8. Security and authorization

- Node catalog requires Employee
- Each node execution requires the project classification to permit the dataset classification
- AutoML is rate-limited per user

## 9. Tests

- Unit: featurization order (split before fit), missing-value handling, fixed seed determinism
- Integration: each ML task trains, evaluates, saves, reloads, predicts deterministically
- Integration: AutoML trial runs and persists results

## 10. Verification commands

```bash
dotnet test tests/Beep.KocAiCommunity.UnitTests --filter "FullyQualifiedName~Ml"
```

## 11. Acceptance gate

- Each ML task trains, evaluates, saves, reloads, predicts deterministically
- AutoML trials persist results via the `IMonitor` adapter (Phase 11)
- Featurization guards prevent fitting on training-test union
- Tests pass

## 12. Risks and deferred work

- Forecasting node requires time-series-specific split logic; verify against sample datasets
- Recommendation and ranking are ML.NET-supported but not O&G priority; defer deeper validation
- Deep learning scenarios (TorchSharp) are deferred
