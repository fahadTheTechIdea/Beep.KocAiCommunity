# `sql-filter` — Filter (SQL)
**Category:** Data · **Ports:** Table → Table · **Handler:** `SqlFilterHandler` (`src/Beep.KocAiCommunity.ML/Nodes/DuckNodeHandlers.cs`)

Keeps only the rows of the working table that satisfy a user-supplied SQL WHERE condition.

## What it does
1. Reads the `where` config value; if blank, the node is skipped and data passes through unchanged.
2. Builds `SELECT * FROM "working" WHERE {where}`, interpolating the `where` fragment raw.
3. Executes the query and makes the filtered rows the new working table.
4. Marked `replay:false` — it is a row op and is NOT replayed onto the eval set at predict time.

## Parameters today
| key | UI control | type | default | required | column/dataset-aware |
|---|---|---|---|---|---|
| where | Text | string | (empty) | required (blank → skip) | No |

## Gaps / plan (to be complete & friendly for non-IT users)
- Add a visual condition builder: column dropdown + operator + value, with AND/OR grouping.
- Keep the raw-SQL WHERE fragment as an advanced fallback for power users.

## Notes
- The `where` fragment is interpolated raw into the SELECT; there is no escaping or validation of the condition text.
- As a row op (`replay:false`), the filter does not affect the fixed eval set at prediction time.
