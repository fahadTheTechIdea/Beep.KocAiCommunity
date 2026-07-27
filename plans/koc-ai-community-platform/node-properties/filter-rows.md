# `filter-rows` — Filter rows
**Category:** Shape · **Ports:** Table → Table · **Handler:** `FilterRowsHandler` (`src/Beep.KocAiCommunity.ML/Nodes/MlPrepareHandlers.cs`)

Keeps only rows where a chosen numeric column falls within a range.

## What it does
1. Reads `column` (a single column) plus `min` and `max` bounds.
2. Validates the column in-handler via `HasColumn` (it is free-text, NOT filtered through FeatureNames).
3. Applies ML.NET `FilterRowsByColumn`, which requires a NUMERIC column.
4. Keeps rows where `column` is in the half-open interval `[min, max)`.

## Parameters today
| key | UI control | type | default | range / clamp | required | column-aware |
|---|---|---|---|---|---|---|
| column | Column | text | — | must exist (`HasColumn`) and be numeric | yes | yes (single, free-text validated) |
| min | Number | number | NegativeInfinity | — | no (has default) | no |
| max | Number | number | PositiveInfinity | — | no (has default) | no |

## Gaps / plan (to be complete & friendly for non-IT users)
- `column` → numeric-column dropdown so users pick a valid numeric column rather than typing a name.
- Make `min`/`max` clearly optional in the UI (defaults are ±Infinity = no bound on that side).

## Notes
- The bound is half-open `[min, max)`: rows equal to `min` are kept, rows equal to `max` are excluded.
- `column` is NOT filtered through FeatureNames, so it can reference the label or id column — there is no leakage guard here (filtering on the label is allowed).
- `column` is free-text (not a `Columns` picker), so the handler checks presence and fails with a clear message on a typo.
- Requires a numeric column (`FilterRowsByColumn` constraint); non-numeric columns are not valid targets. Shape handler operating directly on rows (no fitted state).
