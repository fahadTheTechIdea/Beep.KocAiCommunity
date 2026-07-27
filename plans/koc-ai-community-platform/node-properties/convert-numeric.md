# `convert-numeric` — Cast to number
**Category:** Prepare · **Ports:** Table → Table · **Handler:** `ConvertNumericHandler` (`src/Beep.KocAiCommunity.ML/Nodes/MlPrepareHandlers.cs`)

Casts selected text feature columns to numeric (`Single`) so they can be used by numeric transforms and models.

## What it does
1. Reads `columns`; if blank, targets ALL text feature columns.
2. Filters the requested columns through FeatureNames so label/id/__fold are excluded.
3. Converts each resolved text column to `Single` (numeric).
4. Runs through FitTransform (fit on TRAIN FOLD only, then replay).

## Parameters today
| key | UI control | type | default | range / clamp | required | column-aware |
|---|---|---|---|---|---|---|
| columns | Columns | column list | blank (= all text feature columns) | filtered through FeatureNames | no | yes (multiple text feature columns) |

## Gaps / plan (to be complete & friendly for non-IT users)
- `columns` picker should offer only TEXT feature columns (the ones that can actually be cast), not every column.
- Show which columns are text vs. already numeric so users understand what "all" resolves to.

## Notes
- Label and id columns are always excluded via FeatureNames — casting the label here is not possible by design.
- TextFeatures/NumericFeatures are the text/numeric subset of features; this node moves columns from the text subset into the numeric subset.
- Blank `columns` means "all text feature columns", which is the friendly default for cleaning up string-typed numbers.
