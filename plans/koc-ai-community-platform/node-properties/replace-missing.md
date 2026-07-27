# `replace-missing` — Replace missing
**Category:** Transform · **Ports:** Table → Table · **Handler:** `ReplaceMissingHandler` (`src/Beep.KocAiCommunity.ML/Nodes/MlTransformHandlers.cs`)

Imputes missing numeric values (common in PI sensor gaps) using a chosen statistic.

## What it does
1. Reads the `mode` parameter (default `mean`; options `mean`/`min`/`max`; runtime also accepts `minimum`/`maximum` aliases; anything unrecognized falls back to Mean).
2. Selects all `NumericFeatures` (Single-typed feature columns, excluding label/id/`__fold`).
3. Applies `ReplaceMissingValues` with the chosen statistic to those columns in place.
4. Runs through `FitTransform`: fit on the TRAIN FOLD ONLY and records a replay for the validation/test folds.

## Parameters today
| key | UI control | type | default | range / clamp | required | column-aware |
|---|---|---|---|---|---|---|
| `mode` | select dropdown | string | `mean` | `mean` / `min` / `max` (aliases `minimum`/`maximum`; else → Mean) | no | acts on all numeric features |

## Gaps / plan (to be complete & friendly for non-IT users)
- Expose a Median mode (ML.NET supports it) — often the friendliest default for sensor data.
- Add an optional `columns` picker (blank = all numeric).
- Add constant-value imputation (fill with a user-supplied number).

## Notes
- Runs through `FitTransform` — fit on TRAIN FOLD ONLY plus recorded replay, so the imputed statistic comes only from training data (no leakage).
- Operates only on `NumericFeatures`; label, id, and `__fold` are excluded automatically.
