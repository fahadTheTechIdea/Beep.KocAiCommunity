# `select-columns` — Select columns
**Category:** Transform · **Ports:** Table → Table · **Handler:** `SelectColumnsHandler` (`src/Beep.KocAiCommunity.ML/Nodes/MlTransformHandlers.cs`)

Keeps only the feature columns you choose and drops the rest, while always protecting label, id, and fold columns.

## What it does
1. Reads the `columns` parameter (the list of feature columns to keep); if blank, the node is skipped and the table passes through unchanged.
2. Computes the complement over `FeatureNames` only, so label/id/`__fold` columns are never eligible to be dropped.
3. Drops every feature column that is not in the keep list.
4. Runs through `FitTransform`: fit on the TRAIN FOLD ONLY and records a replay for the validation/test folds.

## Parameters today
| key | UI control | type | default | range / clamp | required | column-aware |
|---|---|---|---|---|---|---|
| `columns` | multi-select column picker | string list | (blank) | n/a | required in schema (cosmetic) | multi, from input FEATURE columns |

## Gaps / plan (to be complete & friendly for non-IT users)
- The `required` flag is cosmetic: a blank value silently skips the node instead of raising a validation error. Either enforce it or relabel it "optional (blank keeps all)".
- Add a friendly hint that label/id/fold columns are protected and cannot be dropped here.

## Notes
- Runs through `FitTransform` — fit on TRAIN FOLD ONLY plus recorded replay, so there is no leakage across folds.
- The complement is computed over `FeatureNames`, guaranteeing label, id, and `__fold` survive regardless of selection.
