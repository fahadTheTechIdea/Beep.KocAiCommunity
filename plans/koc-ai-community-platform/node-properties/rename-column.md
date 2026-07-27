# `rename-column` — Rename column
**Category:** Prepare · **Ports:** Table → Table · **Handler:** `RenameColumnHandler` (`src/Beep.KocAiCommunity.ML/Nodes/MlPrepareHandlers.cs`)

Renames one existing column to a new name.

## What it does
1. Reads `from` (the existing column) and `to` (the new name).
2. Skips (no-op) if either value is blank, or if `from` is not present in the data.
3. Copies the `from` column to a new column named `to` (`CopyColumns(to, from)`).
4. Drops the original `from` column (`DropColumns(from)`).
5. Runs through FitTransform (fit on TRAIN FOLD only, then replay), though the rename itself carries no learned state.

## Parameters today
| key | UI control | type | default | range / clamp | required | column-aware |
|---|---|---|---|---|---|---|
| from | Text | text | — | must be an existing column | yes | yes (single input column) |
| to | Text | text | — | — | yes | no (free text) |

## Gaps / plan (to be complete & friendly for non-IT users)
- `from` → column-single-dropdown populated from the incoming schema so users pick rather than type an exact name.
- Inline validation / preview of the resulting column name to catch typos and collisions before running.

## Notes
- No leakage risk: a rename is a pure structural operation with no fitted statistics.
- If `from` is blank, `to` is blank, or `from` does not exist, the node is a silent no-op (data passes through unchanged).
- FeatureNames excludes label/id/__fold; the rename operates by column name regardless of feature role. Renaming the label column orphans its role and is caught downstream by `RequireLabel`.
