# `pca` — PCA
**Category:** Transform · **Ports:** Table → Table · **Handler:** `PcaHandler` (`src/Beep.KocAiCommunity.ML/Nodes/MlTransformHandlers.cs`)

Reduces numeric features to N principal components.

## What it does
1. Reads the `rank` parameter (default 2, min 1, clamped to `Math.Clamp(..., 1, numericFeatureCount)`).
2. Requires at least 2 numeric features; otherwise it skips with "need >=2 numeric columns".
3. Concatenates all `NumericFeatures`, runs `ProjectToPrincipalComponents`, drops the originals, and replaces them with a single `Pca` vector column.
4. Runs through `FitTransform`: fit on the TRAIN FOLD ONLY and records a replay for the validation/test folds.

## Parameters today
| key | UI control | type | default | range / clamp | required | column-aware |
|---|---|---|---|---|---|---|
| `rank` | number field | integer | `2` | min 1, clamped 1–(#numeric features) | no | acts on all numeric features |

## Gaps / plan (to be complete & friendly for non-IT users)
- Expose overSampling, ensureZeroMean/centering, and the seed.
- The upper bound of `rank` is data-dependent (numeric feature count); the UI can only hint at it rather than enforce it before the data is known.

## Notes
- Runs through `FitTransform` — fit on TRAIN FOLD ONLY plus recorded replay, so components come only from training data (no leakage).
- Needs >= 2 numeric features or it skips. Outputs a single `Pca` vector and drops the original numeric columns.
