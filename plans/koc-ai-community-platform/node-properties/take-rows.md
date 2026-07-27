# `take-rows` — Take first N
**Category:** Shape · **Ports:** Table → Table · **Handler:** `TakeRowsHandler` (`src/Beep.KocAiCommunity.ML/Nodes/MlPrepareHandlers.cs`)

Keeps only the first N rows of the table.

## What it does
1. Reads `count` (N), flooring it to at least 1 (`Math.Max(1, ...)`); there is no upper bound.
2. HARD FAILS if the pipeline has already been split (`ctx.HasSplit` / `RowSampleAfterSplit`), because a positional take would drop the held-out fold.
3. Otherwise takes the first N rows directly (Shape handlers operate on rows, not through FitTransform).

## Parameters today
| key | UI control | type | default | range / clamp | required | column-aware |
|---|---|---|---|---|---|---|
| count | Integer | integer | 1000 | floored to >= 1 (`Math.Max(1, ...)`), no upper bound | no (has default) | no |

## Gaps / plan (to be complete & friendly for non-IT users)
- Explain up front (in the UI) that take must run before the split, so users understand the hard-fail rather than hitting it.
- Consider showing the current row count so N can be chosen meaningfully.

## Notes
- HARD FAIL after split: rows are ordered train-then-test, so `take-rows(N <= trainCount)` would silently drop the entire test fold. Place take-rows before the split.
- Positional operation (first N rows in current order) — not random; pair with `shuffle` first if you want a random subset.
- Not column-aware; operates on whole rows.
