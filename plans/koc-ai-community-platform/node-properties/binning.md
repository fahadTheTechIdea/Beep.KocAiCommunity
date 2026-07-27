# `binning` — Bin values
**Category:** Transform · **Ports:** Table → Table · **Handler:** `BinningHandler` (`src/Beep.KocAiCommunity.ML/Nodes/MlTransformHandlers.cs`)

Buckets numeric features into a bounded number of bins.

## What it does
1. Reads the `bins` parameter (default 10, clamped to `Math.Clamp(..., 2, 255)`).
2. Selects all `NumericFeatures` (Single-typed feature columns, excluding label/id/`__fold`).
3. Applies `NormalizeBinning(maximumBinCount = bins)` to those columns in place.
4. Runs through `FitTransform`: fit on the TRAIN FOLD ONLY and records a replay for the validation/test folds.

## Parameters today
| key | UI control | type | default | range / clamp | required | column-aware |
|---|---|---|---|---|---|---|
| `bins` | number field | integer | `10` | clamped 2–255 | no | acts on all numeric features |

## Gaps / plan (to be complete & friendly for non-IT users)
- Add an optional `columns` picker (blank = all numeric) so users can bin a subset instead of every numeric feature.

## Notes
- Runs through `FitTransform` — fit on TRAIN FOLD ONLY plus recorded replay, so bin edges come only from training data (no leakage).
- Operates only on `NumericFeatures`; label, id, and `__fold` are excluded automatically.
