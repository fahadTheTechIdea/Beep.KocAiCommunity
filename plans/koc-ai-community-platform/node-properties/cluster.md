# `cluster` — Cluster (k-means)
**Category:** Model · **Ports:** Table → Model · **Handler:** `MlModelHandlers` (`src/Beep.KocAiCommunity.ML/Nodes/MlModelHandlers.cs`, factories in `MlModelOps.cs`)

Unsupervised grouping of rows into k clusters — no label required.

## What it does
1. In predict mode → skip.
2. Features = numeric `FeatureNames`; if empty → skip.
3. Fits `KMeans(numberOfClusters = k)` on `FoldTrainView`.
4. Reports `AverageDistance` + `DaviesBouldinIndex`.

## Parameters today
| key | UI control | type | default | range / clamp | required | column-aware |
|---|---|---|---|---|---|---|
| `clusters` | Integer | int | `3` | `Math.Clamp(..., 2, 20)` | no | no |

## Gaps / plan (to be complete & friendly for non-IT users)
- Expose **maxIterations** (`MaximumNumberOfIterations`).
- Expose **initialization** (Select: `KMeansYinyang` default / `Random` / `KMeansPlusPlus`).
- Optional **feature-columns picker** so users can cluster on a chosen subset.

## Notes
- Unsupervised — **no label**; it is the exception to the `RequireLabel` guard.
- `clusters` is read via `ReadDouble` + cast (not `HpInt`), so the `0 = unset` convention does not apply here.
- Fits on the train fold only (`FoldTrainView`).
