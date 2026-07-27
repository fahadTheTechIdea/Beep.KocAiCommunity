# `combine-columns` — Merge columns
**Category:** Prepare · **Ports:** Table → Table · **Handler:** `CombineColumnsHandler` (`src/Beep.KocAiCommunity.ML/Nodes/MlPrepareHandlers.cs`)

Concatenates several numeric columns into a single combined feature vector.

## What it does
1. Reads `columns`; if blank, targets ALL numeric feature columns.
2. Resolves the columns and requires at least 2 (if fewer resolve, the node skips as a no-op).
3. Concatenates the resolved columns into a single vector column named `Combined`.
4. Drops the original columns that were merged.
5. Runs through FitTransform (fit on TRAIN FOLD only, then replay).

## Parameters today
| key | UI control | type | default | range / clamp | required | column-aware |
|---|---|---|---|---|---|---|
| columns | Columns | column list | blank (= all numeric features) | needs >= 2 resolved columns or skip | no | yes (multiple numeric features) |

## Gaps / plan (to be complete & friendly for non-IT users)
- `columns` picker should offer only NUMERIC feature columns and warn when fewer than 2 are selected (since the node will otherwise silently skip).
- Let the user name the output vector instead of always producing `Combined`.

## Notes
- Requires at least 2 resolved columns; with 0 or 1 the node is a silent no-op.
- Column resolution runs against numeric features only; label/id/__fold are excluded via FeatureNames (roles safe).
- Output is a single vector column (`Combined`); the source columns are dropped, so downstream nodes see the merged vector, not the originals.
