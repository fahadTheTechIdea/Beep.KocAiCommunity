# `dataset` — Dataset
**Category:** Source · **Ports:** None → Table · **Handler:** `MlModelHandlers` (`src/Beep.KocAiCommunity.ML/Nodes/MlModelHandlers.cs`, factories in `MlModelOps.cs`)

The pipeline's data entry point — surfaces the loaded table and its row/column counts to everything downstream.

## What it does
1. Acts as a pure passthrough — does not transform, filter, or copy the data.
2. Returns `"done"` with detail `"{rows} rows · {feature count} columns"`.
3. Feature count is derived from `FeatureNames` = all columns **except** `LabelColumn`, `IdColumn`, and the internal `__fold` column.

## Parameters today
| key | UI control | type | default | range / clamp | required | column-aware |
|---|---|---|---|---|---|---|
| — | None — acts on all flowing columns. | | | | | |

## Gaps / plan (to be complete & friendly for non-IT users)
- FREE mode: this is where the user should **pick the dataset** — add a dataset/file picker (Select or upload) so a non-IT user chooses their data here.
- COMPETITION mode: the dataset is **fixed by the competition** — the picker should be pre-filled and locked (read-only).
- Consider a lightweight column/schema preview so users can confirm the right data loaded before wiring downstream nodes.

## Notes
- Pure passthrough — introduces no ordering or leakage concerns of its own.
- `__fold` is internal bookkeeping and is deliberately excluded from the reported feature count.
