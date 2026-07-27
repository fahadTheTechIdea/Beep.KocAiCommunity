# `shuffle` — Shuffle rows
**Category:** Shape · **Ports:** Table → Table · **Handler:** `ShuffleHandler` (`src/Beep.KocAiCommunity.ML/Nodes/MlPrepareHandlers.cs`)

Randomly reorders the rows of the table deterministically.

## What it does
1. Applies `ShuffleRows` with a fixed seed (`seed: 1`) to reorder all rows.
2. Returns the shuffled rows directly (Shape handlers operate on rows, not through FitTransform).

## Parameters today
None.

## Gaps / plan (to be complete & friendly for non-IT users)
- Optionally expose the seed so users can reproduce or vary the ordering intentionally.

## Notes
- Deterministic: the fixed `seed: 1` means the same input always shuffles to the same order (reproducible runs).
- No split guard — folds are tracked by the `__fold` value, not row position, so shuffling is safe before or after a split.
- Not column-aware; operates on whole rows.
