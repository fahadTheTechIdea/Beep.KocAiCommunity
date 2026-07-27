# `train` — Train model
**Category:** Model · **Ports:** Table → Model · **Handler:** `MlModelHandlers` (`src/Beep.KocAiCommunity.ML/Nodes/MlModelHandlers.cs`, factories in `MlModelOps.cs`)

Fits a supervised model on the training fold using the pipeline label and numeric features.

## What it does
1. `RequireLabel` — fails if no label column is set.
2. Features = numeric `FeatureNames`; if empty → throw (predict) / fail.
3. `FitModel` on `FoldTrainView` (train fold only, never the held-out test fold).

## Parameters today
| key | UI control | type | default | range / clamp | required | column-aware |
|---|---|---|---|---|---|---|
| `algorithm` | Select | string | `sdca` | options = `MlAlgorithms.All` (task-filtered) | no | no |
| `trees` | Integer | int | `100` | min 1; FastTree/FastForest only | no | no |
| `leaves` | Integer | int | `20` | min 2 | no | no |
| `learningRate` | Number | number | `0.2` | min 0 | no | no |
| `l2` | Number | number | _(none)_ | min 0; blank = trainer default | no | no |

## Gaps / plan (to be complete & friendly for non-IT users)
- Label/ID/Task are **pipeline-level** today (`ctx.LabelColumn` / `ctx.IdColumn` / `ctx.Task`), **not** node config. No per-node label/id/feature config exists.
- FREE mode: node should expose **Target/label column** (Column picker), **ID column** (Column picker), **Task** (Select: Binary / Multiclass / Regression), and optional **Feature columns** (Columns picker, blank = all). COMPETITION mode: pre-filled from the competition and **locked**.
- Per-algorithm hyperparameters (Option A: one node, fields change with the selected algorithm) — see subsections below.
- `minLeaf` (`MinimumExampleCountPerLeaf`) is hardcoded `10`; `NumberOfThreads = 1` is hardcoded. `HpInt`/`HpFloat` treat `0`/negative as "unset → trainer default".

### Per-algorithm hyperparameters (train)
#### SDCA (reg: `Sdca`; binary: `SdcaLogisticRegression`; multiclass: `SdcaMaximumEntropy`)
| field | ML.NET Options | type | default | range | status |
|---|---|---|---|---|---|
| l2 | `L2Regularization` | float | auto | ≥ 0 (0 = auto) | ✅ wired |
| l1 | `L1Regularization` | float | — | ≥ 0 | 🆕 new |
| maxIterations | `MaximumNumberOfIterations` | int | — | ≥ 1 | 🆕 new |

#### L-BFGS (`LbfgsPoissonRegression` / `LbfgsLogisticRegression` / `LbfgsMaximumEntropy`)
| field | ML.NET Options | type | default | range | status |
|---|---|---|---|---|---|
| l2 | `L2Regularization` | float | `1f` | ≥ 0 | ✅ wired |
| l1 | `L1Regularization` | float | `1.0` | ≥ 0 | 🆕 new |
| historySize | `HistorySize` | int | `20` | ≥ 1 | 🆕 new |

#### FastTree (binary + regression only)
| field | ML.NET Options | type | default | range | status |
|---|---|---|---|---|---|
| trees | `NumberOfTrees` | int | `100` | ≥ 1 | ✅ wired |
| leaves | `NumberOfLeaves` | int | `20` | ≥ 2 | ✅ wired |
| minLeaf | `MinimumExampleCountPerLeaf` | int | `10` | ≥ 1 | 🆕 new |
| learningRate | `LearningRate` | float | `0.2` | > 0 | ✅ wired |
| featureFraction / l1 / l2 / maxBins | (optional) | — | — | — | 🆕 new |

#### FastForest (binary + regression only)
| field | ML.NET Options | type | default | range | status |
|---|---|---|---|---|---|
| trees | `NumberOfTrees` | int | `100` | ≥ 1 | ✅ wired |
| leaves | `NumberOfLeaves` | int | `20` | ≥ 2 | ✅ wired |
| minLeaf | `MinimumExampleCountPerLeaf` | int | `10` | ≥ 1 | 🆕 new |
| featureFraction | `FeatureFraction` | float | — | 0–1 | 🆕 new |
| _(no learningRate — bagged)_ | | | | | |

#### AveragedPerceptron (binary only)
| field | ML.NET Options | type | default | range | status |
|---|---|---|---|---|---|
| learningRate | `LearningRate` | float | `1.0` | > 0 | 🆕 new |
| numberOfIterations | `NumberOfIterations` | int | `10` | ≥ 1 | 🆕 new |
| l2 | `L2Regularization` | float | — | ≥ 0 | 🆕 new (optional) |

_Currently **nothing** is set for AveragedPerceptron._

#### NaiveBayes (multiclass only)
No tunable hyperparameters — dropdown selection only.

### Candidate NEW algorithms to add
- **LightGbm** (binary / regression / multiclass)
- **Gam** (binary / regression)
- **SgdCalibrated** (binary, calibrated)
- **OneVersusAll** (multiclass — lets FastTree/FastForest work for multiclass; today they silently fall back to SDCA for multiclass)
- **Ols / OnlineGradientDescent** (regression)

## Notes
- No per-node label/id/feature config today — all three come from pipeline context.
- Training uses `FoldTrainView` only, so the held-out test fold never leaks into the fit.
- `HpInt`/`HpFloat` coerce `0`/negative inputs to "unset", falling back to the trainer default.
