# `shuffle` — Shuffle rows

**Category:** Shape · **Ports:** Table → Table · **Handler:** `ShuffleHandler`

Randomly reorder the rows (deterministic seed 1).

## Parameters
_None._

## Panel on click
"This node has no settings."

## Notes
Safe after `split` — folds are tracked by the `__fold` value, not row position.
