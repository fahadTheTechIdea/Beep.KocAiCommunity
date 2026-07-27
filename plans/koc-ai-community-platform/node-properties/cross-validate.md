# `cross-validate` — Cross-validate
**Category:** Model · **Ports:** Table → Metrics · **Handler:** `MlModelHandlers` (`src/Beep.KocAiCommunity.ML/Nodes/MlModelHandlers.cs`, factories in `MlModelOps.cs`)

K-fold validation for a more honest metric, run entirely inside the train fold using the same trainer as `train`.

## What it does
1. In predict mode → skip.
2. `RequireLabel` — fails if no label column is set.
3. Uses the **same** `MlModelOps.Trainer` / `MulticlassTrainer` as `train`.
4. `CrossValidate` on the train fold only.
5. Reports: regression → mean R²; multiclass → mean micro-accuracy; binary → mean accuracy (NonCalibrated).

## Parameters today
| key | UI control | type | default | range / clamp | required | column-aware |
|---|---|---|---|---|---|---|
| `folds` | Integer | int | `5` | `Math.Clamp(..., 2, 10)` | no | no |
| `algorithm` | Select | string | `sdca` | options = `MlAlgorithms.All` (task-filtered) | no | no |
| `trees` | Integer | int | `100` | min 1; FastTree/FastForest only | no | no |
| `leaves` | Integer | int | `20` | min 2 | no | no |
| `learningRate` | Number | number | `0.2` | min 0 | no | no |
| `l2` | Number | number | _(none)_ | min 0; blank = trainer default | no | no |

## Gaps / plan (to be complete & friendly for non-IT users)
- Should gain the **same per-algorithm hyperparameters** as `train` (see `train.md` per-algorithm tables) since it shares the trainer factory.
- Multiclass path ignores `trees` / `leaves` / `learningRate` — surface this to the user or hide those fields for multiclass tasks.
- As with `train`, FREE vs COMPETITION should drive whether label/task come from a picker or are locked.

## Notes
- Shares `MlModelOps.Trainer` / `MulticlassTrainer` with `train`, so it stays algorithm-consistent with the model node.
- Runs on the train fold only — the held-out test fold is never touched.
