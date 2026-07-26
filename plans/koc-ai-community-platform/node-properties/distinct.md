# `distinct` — Deduplicate

**Category:** Data · **Ports:** Table → Table · **Handler:** `DistinctHandler`

Remove duplicate rows (`SELECT DISTINCT *`).

## Parameters
_None._

## Panel on click
"This node has no settings."

## Notes
Rows with a differing `__fold` value stay distinct, so folds survive.
