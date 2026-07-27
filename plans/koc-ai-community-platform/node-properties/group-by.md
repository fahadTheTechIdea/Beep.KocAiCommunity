# `group-by` — Group & aggregate
**Category:** Data · **Ports:** Table → Table · **Handler:** `GroupByHandler` (`src/Beep.KocAiCommunity.ML/Nodes/DuckNodeHandlers.cs`)

Collapses the working table into one row per group, computing user-defined aggregate expressions.

## What it does
1. Reads `groupBy` (the grouping key columns) and `aggregations` (the aggregate expressions); if either is blank, the node is skipped.
2. Quote-escapes each grouping key, and takes the `aggregations` value as raw comma-separated aggregate expressions with aliases (e.g. `AVG(pressure) AS avg_p`).
3. Builds `SELECT keys, {aggregations} FROM "working" GROUP BY keys` and executes it, making the aggregated result the new working table.
4. Marked `replay:false` — it is a row op and is NOT replayed onto the eval set at predict time.

## Parameters today
| key | UI control | type | default | required | column/dataset-aware |
|---|---|---|---|---|---|
| groupBy | Columns | string (multi) | (empty) | required (blank → skip) | Yes — keys Quote-escaped, validated against scope |
| aggregations | Text | string | (empty) | required (blank → skip) | No — raw SQL aggregate exprs |

## Gaps / plan (to be complete & friendly for non-IT users)
- Biggest gap: a repeatable aggregate builder — function dropdown (COUNT / SUM / AVG / MIN / MAX / MEDIAN / STDDEV) + column picker + output-name field.
- Keep the raw `aggregations` SQL as an advanced fallback.

## Notes
- Any column not present in the SELECT is dropped, including the label column and `__fold` — this can silently break downstream label/fold handling.
- Grouping keys are Quote-escaped; the `aggregations` text is interpolated raw.
