# `cross-validate` — Cross-validate

**Category:** Model · **Ports:** Table → Metrics · **Handler:** `CrossValidateHandler` (+ `MlModelOps`)

K-fold validation for a more honest metric (runs inside the train fold).

## Parameters
| `key` | Label | Type | Default | Required | Options / Range | In UI today |
|---|---|---|---|---|---|---|
| `folds` | Folds | Number | `5` | no | clamped 2–10 | ✅ yes |
| `algorithm` | Algorithm | Select | `sdca` | no | `sdca`, `lbfgs`, `fasttree`, `fastforest`, `perceptron`, `naivebayes` | ✅ yes |
| `trees` | Trees (fasttree/fastforest) | Number | `100` | no | > 0 | ✅ yes |
| `leaves` | Leaves per tree (fasttree/fastforest) | Number | `20` | no | > 0 | ✅ yes |
| `learningRate` | Learning rate (fasttree) | Number | `0.2` | no | > 0 | ✅ yes |
| `l2` | L2 regularization (sdca/lbfgs) | Number | blank = trainer default | no | > 0 | ✅ yes |

## Panel on click
`folds` + `algorithm` + the four hyperparameters, each pre-filled with its default.

## Notes
Resolved in Phase 21a: uses the same trainer factory as `train` (`MlModelOps.Trainer`/`MulticlassTrainer`), so it now declares the identical `algorithm` + hyperparameter set and the inspector renders them. Skipped in predict mode. Covered by the `NodePropertyDriftTests` guard alongside `train`.
