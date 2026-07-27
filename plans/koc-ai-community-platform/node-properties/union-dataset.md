# `union-dataset` — Append another dataset
**Category:** Data · **Ports:** Table → Table · **Handler:** `UnionDatasetHandler` (`src/Beep.KocAiCommunity.ML/Nodes/DuckNodeHandlers.cs`)

Appends the rows of a second attached dataset onto the working table, aligning columns by name.

## What it does
1. Reads `datasetId` (a GUID) and resolves the attached dataset to its `ds_N` table via `SecondaryTable(id)`; if it does not resolve, the node is skipped.
2. Combines the two tables with `UNION ALL BY NAME`, so columns are matched by name and any column missing on one side is filled with NULL.
3. If the current data is labelled but the appendee lacks the label column, the node HARD FAILS.
4. Repairs `__fold`: appended rows are forced to train fold 0.
5. Marked `replay:false` — it is a row op and is NOT replayed onto the eval set at predict time.

## Parameters today
| key | UI control | type | default | required | column/dataset-aware |
|---|---|---|---|---|---|
| datasetId | Dataset | GUID | (empty) | required (unresolved → skip) | Yes — must resolve or throw |

## Gaps / plan (to be complete & friendly for non-IT users)
- Add an append mode choice: append-all vs distinct.
- Add explicit column-mapping/preview so the NULL-fills from name mismatches are visible before running.
- Let the user choose the fold (train/test) for appended rows instead of always forcing train fold 0.

## Notes
- `UNION ALL BY NAME` matches columns by name; missing columns become NULL.
- Appending unlabelled data onto labelled data is a hard failure.
- Appended rows are always assigned train fold 0 via the `__fold` repair.
