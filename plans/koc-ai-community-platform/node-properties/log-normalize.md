# `log-normalize` — Log normalize
**Category:** Transform · **Ports:** Table → Table · **Handler:** `LogNormalizeHandler` (`src/Beep.KocAiCommunity.ML/Nodes/MlTransformHandlers.cs`)

Log-transforms then rescales numeric features — good for skewed rates and long-tailed values.

## What it does
1. Selects all `NumericFeatures` (Single-typed feature columns, excluding label/id/`__fold`).
2. Applies `NormalizeLogMeanVariance` to those columns in place.
3. Runs through `FitTransform`: fit on the TRAIN FOLD ONLY and records a replay for the validation/test folds.

## Parameters today
| key | UI control | type | default | range / clamp | required | column-aware |
|---|---|---|---|---|---|---|
None — acts on all numeric/text feature columns automatically.

## Gaps / plan (to be complete & friendly for non-IT users)
- Add an optional `columns` picker (blank = all numeric) so users can log-normalize a subset instead of every numeric feature.

## Notes
- Runs through `FitTransform` — fit on TRAIN FOLD ONLY plus recorded replay, so statistics come only from training data (no leakage).
- Operates only on `NumericFeatures`; label, id, and `__fold` are excluded automatically.
