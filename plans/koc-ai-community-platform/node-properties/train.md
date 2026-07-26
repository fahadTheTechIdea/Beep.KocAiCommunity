# `train` — Train model

**Category:** Model · **Ports:** Table → Model · **Handler:** `TrainHandler` (+ `MlModelOps`)

Fits a supervised model on the training features and label.

## Parameters
| `key` | Label | Type | Default | Required | Options / Range | In UI today |
|---|---|---|---|---|---|---|
| `algorithm` | Algorithm | Select | `sdca` | no | `sdca`, `lbfgs`, `fasttree`, `fastforest`, `perceptron` (binary), `naivebayes` (multiclass) | ✅ yes |
| `trees` | Trees (fasttree/fastforest) | Number | `100` | no | > 0 (FastTree/FastForest) | ✅ yes |
| `leaves` | Leaves per tree (fasttree/fastforest) | Number | `20` | no | > 0 (FastTree/FastForest) | ✅ yes |
| `learningRate` | Learning rate (fasttree) | Number | `0.2` | no | > 0 (FastTree) | ✅ yes |
| `l2` | L2 regularization (sdca/lbfgs) | Number | blank = trainer default (`1` for lbfgs) | no | > 0 (SDCA/LBFGS) | ✅ yes |

## Panel on click
The `algorithm` dropdown + four number fields, each pre-filled with the default above and labelled with the algorithm it affects.

## Notes
Resolved in Phase 21a: `trees`/`leaves`/`learningRate`/`l2` are read by the executor (`MlModelOps.cs`) **and** declared on the descriptor, so the descriptor-driven inspector renders them automatically. `perceptron`/`naivebayes` are now offered in the algorithm list. Hyperparameters are algorithm-conditional — irrelevant ones are ignored by the trainer (Phase 21b would hide them per selected algorithm). The `NodePropertyDriftTests` guard fails if any config key the executor reads is ever dropped from the descriptor again.
