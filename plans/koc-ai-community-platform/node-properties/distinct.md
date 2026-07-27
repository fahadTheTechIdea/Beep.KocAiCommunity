# `distinct` — Deduplicate
**Category:** Data · **Ports:** Table → Table · **Handler:** `DistinctHandler` (`src/Beep.KocAiCommunity.ML/Nodes/DuckNodeHandlers.cs`)

Removes duplicate rows from the working table.

## What it does
1. Takes no configuration.
2. Builds and executes `SELECT DISTINCT * FROM "working"`, making the deduplicated rows the new working table.
3. Marked `replay:false` — it is a row op and is NOT replayed onto the eval set at predict time.

## Parameters today
| key | UI control | type | default | required | column/dataset-aware |
|---|---|---|---|---|---|

None.

## Gaps / plan (to be complete & friendly for non-IT users)
- Add an optional "distinct on subset of columns" picker so users can dedupe on chosen keys rather than the whole row.

## Notes
- Deduplication is over all columns, including `__fold`, so rows differing only by fold assignment are treated as distinct.
