# `global-contrast` — Global contrast
**Category:** Prepare · **Ports:** Table → Table · **Handler:** `GlobalContrastHandler` (`src/Beep.KocAiCommunity.ML/Nodes/MlPrepareHandlers.cs`)

Applies global contrast normalization to each row's numeric feature vector.

## What it does
1. Assembles the numeric feature vector for each row.
2. Skips with a "no numeric columns" result if there are no numeric features.
3. Applies `NormalizeGlobalContrast` to the vector per row.
4. Runs through FitTransform (fit on TRAIN FOLD only, then replay).

## Parameters today
None.

## Gaps / plan (to be complete & friendly for non-IT users)
- None required. Optionally add a columns picker so users can target a chosen subset of numeric features.

## Notes
- Shares its mechanics with `lp-normalize` (same vector-normalize helper), differing only in the ML.NET transform used (`NormalizeGlobalContrast`).
- Row-wise: each row is normalized independently.
- No-op (skip) when there are no numeric feature columns; operates on the numeric feature subset only (label/id/__fold excluded).
