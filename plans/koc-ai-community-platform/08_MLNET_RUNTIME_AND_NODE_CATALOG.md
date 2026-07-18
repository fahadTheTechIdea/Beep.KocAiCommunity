# Phase 08 — ML.NET Runtime and Node Catalog

**Status:** 🟡 PLANNING
**Dependencies:** Phase 03, Phase 07, Phase 07a
**Goal:** Define the ML.NET runtime contracts, register the O&G node catalog, and validate deterministic sample workflows.

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
