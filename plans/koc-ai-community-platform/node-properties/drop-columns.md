# `drop-columns` — Drop columns
**Category:** Transform · **Ports:** Table → Table · **Handler:** `DropColumnsHandler` (`src/Beep.KocAiCommunity.ML/Nodes/MlTransformHandlers.cs`)

Removes the columns you list (ids, noise, leaky fields) from the table.

## What it does
1. Reads the `columns` parameter (the list of columns to drop).
2. Filters the list by `HasColumn`, then drops each matching column.
3. Because it operates over ALL input columns, it can drop label, id, or `__fold` columns — which may later trip downstream `RequireLabel` / predict id-guard / fold guards.
4. Runs through `FitTransform`: fit on the TRAIN FOLD ONLY and records a replay for the validation/test folds.

## Parameters today
| key | UI control | type | default | range / clamp | required | column-aware |
|---|---|---|---|---|---|---|
| `columns` | multi-select column picker | string list | (blank) | n/a | required in schema | multi, from ALL input columns |

## Gaps / plan (to be complete & friendly for non-IT users)
- Warn (or block) when a label, id, or `__fold` column is chosen, since dropping it breaks downstream training/prediction.
- Explain the difference from Select columns so non-IT users pick the right one.

## Notes
- Runs through `FitTransform` — fit on TRAIN FOLD ONLY plus recorded replay, so there is no leakage across folds.
- Unlike Select columns, this node can drop ANY column including protected roles; the safety net is only the downstream guards.
