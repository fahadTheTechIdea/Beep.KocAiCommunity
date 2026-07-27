# `sort` — Sort
**Category:** Data · **Ports:** Table → Table · **Handler:** `SortHandler` (`src/Beep.KocAiCommunity.ML/Nodes/DuckNodeHandlers.cs`)

Reorders the rows of the working table by a user-supplied ORDER BY clause, with deterministic tie-breaking.

## What it does
1. Reads the `orderBy` config value; if blank, the node is skipped and data passes through unchanged.
2. Builds `SELECT * FROM "working" ORDER BY {orderBy}`, interpolating the `orderBy` fragment raw (e.g. `pressure DESC`).
3. Appends every input column (Quote-escaped) after the user clause as tie-breakers, guaranteeing a total deterministic ordering.
4. Marked `replay:false` — it is a row op and is NOT replayed onto the eval set at predict time.

## Parameters today
| key | UI control | type | default | required | column/dataset-aware |
|---|---|---|---|---|---|
| orderBy | Text | string | (empty) | required (blank → skip) | No — raw SQL ORDER BY fragment |

## Gaps / plan (to be complete & friendly for non-IT users)
- Add repeatable sort keys: column picker + ASC/DESC toggle + NULLS FIRST/LAST option per key.

## Notes
- The `orderBy` fragment is interpolated raw; the appended per-column tie-breakers are Quote-escaped.
- Tie-break appending ensures runs are reproducible regardless of the user's clause.
