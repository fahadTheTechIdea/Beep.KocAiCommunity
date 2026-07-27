# `sql` — SQL query
**Category:** Data · **Ports:** Table → Table · **Handler:** `SqlHandler` (`src/Beep.KocAiCommunity.ML/Nodes/DuckNodeHandlers.cs`)

Runs a full, user-authored SQL SELECT statement over the current working table.

## What it does
1. Reads the `sql` config value; if blank, the node is skipped and data passes through unchanged.
2. Treats the value as the entire SELECT statement over the DuckDB table named `working` and passes it verbatim to the engine (no wrapping, no rewriting).
3. Executes the query and makes its result the new working table.
4. Marked `replay:true` — it is treated as a column-adding op and is replayed onto the fixed eval set at predict time.

## Parameters today
| key | UI control | type | default | required | column/dataset-aware |
|---|---|---|---|---|---|
| sql | Text | string | (empty) | required (blank → skip) | No |

## Gaps / plan (to be complete & friendly for non-IT users)
- Provide a proper SQL editor with a live schema panel showing the columns of `working`.
- Add a validate/preview button so non-IT users can see results and errors before running the pipeline.

## Notes
- The value is passed verbatim over `working`; free-text SQL is interpolated raw with no escaping.
- If the SELECT drops the label column, a downstream node fails via `RequireLabel`.
- Because `replay:true`, the exact SELECT is re-applied to the eval set at prediction time.
